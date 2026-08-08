use crate::{OpenSeriesError, Result};
use hidapi::{HidApi, HidDevice, HidError};
use std::ffi::CString;
use std::io::ErrorKind;
use std::sync::Arc;

pub(crate) struct HidTransport {
    api: Arc<HidApi>,
    path: CString,
    device: Option<HidDevice>,
    timeout_ms: i32,
    input_length: usize,
    output_length: usize,
    feature_length: usize,
    output_report: Vec<u8>,
    feature_report: Vec<u8>,
    input_report: Vec<u8>,
}

impl HidTransport {
    pub(crate) fn new(
        api: Arc<HidApi>,
        path: CString,
        device: Option<HidDevice>,
        timeout_ms: i32,
        sizes: ReportSizes,
    ) -> Self {
        Self {
            api,
            path,
            device,
            timeout_ms,
            input_length: report_length_or_default(sizes.input),
            output_length: report_length_or_default(sizes.output),
            feature_length: report_length_or_default(sizes.feature),
            output_report: Vec::new(),
            feature_report: Vec::new(),
            input_report: Vec::new(),
        }
    }

    pub(crate) fn write_output(
        &mut self,
        command: &[u8],
        command_offset: usize,
        minimum_report_length: usize,
    ) -> Result<()> {
        let length = self.output_length.max(minimum_report_length);
        prepare_report(
            &mut self.output_report,
            command,
            command_offset,
            length,
            "Output",
        )?;
        self.ensure_device()?;
        let result = (|| {
            let device = self.device.as_ref().expect("HID device must be open");
            let written = device.write(&self.output_report)?;
            if written != self.output_report.len() {
                return Err(HidError::IncompleteSendError {
                    sent: written,
                    all: self.output_report.len(),
                });
            }
            Ok(())
        })();
        self.finish_operation(result)
    }

    pub(crate) fn write_feature(
        &mut self,
        command: &[u8],
        command_offset: usize,
        minimum_report_length: usize,
    ) -> Result<()> {
        let length = self.feature_length.max(minimum_report_length);
        prepare_report(
            &mut self.feature_report,
            command,
            command_offset,
            length,
            "Feature",
        )?;
        self.ensure_device()?;
        let result = self
            .device
            .as_ref()
            .expect("HID device must be open")
            .send_feature_report(&self.feature_report);
        self.finish_operation(result)
    }

    pub(crate) fn write_output_and_read(
        &mut self,
        command: &[u8],
        response_buffer_length: usize,
        command_offset: usize,
        minimum_report_length: usize,
        normalize: Option<u8>,
    ) -> Result<Vec<u8>> {
        let length = self.output_length.max(minimum_report_length);
        prepare_report(
            &mut self.output_report,
            command,
            command_offset,
            length,
            "Output",
        )?;
        self.input_report
            .resize(response_buffer_length.max(self.input_length), 0);
        self.ensure_device()?;
        let result = (|| {
            let device = self.device.as_ref().expect("HID device must be open");
            let written = device.write(&self.output_report)?;
            if written != self.output_report.len() {
                return Err(HidError::IncompleteSendError {
                    sent: written,
                    all: self.output_report.len(),
                });
            }
            let read = device.read_timeout(&mut self.input_report, self.timeout_ms)?;
            if read == 0 {
                return Err(HidError::IoError {
                    error: std::io::Error::new(ErrorKind::TimedOut, "HID read timed out"),
                });
            }
            let response = &self.input_report[..read];
            let start = normalized_response_start(response, normalize);
            Ok(response[start..].to_vec())
        })();
        self.finish_operation(result)
    }

    fn ensure_device(&mut self) -> Result<()> {
        if self.device.is_none() {
            self.device = Some(self.api.open_path(&self.path).map_err(map_hid_error)?);
        }
        Ok(())
    }

    fn finish_operation<T>(&mut self, result: hidapi::HidResult<T>) -> Result<T> {
        match result {
            Ok(value) => Ok(value),
            Err(error) => {
                let error = map_hid_error(error);
                if matches!(error, OpenSeriesError::Disconnected) {
                    self.device = None;
                }
                Err(error)
            }
        }
    }
}

fn report_length_or_default(length: usize) -> usize {
    if length == 0 { 64 } else { length }
}

fn normalized_response_start(response: &[u8], expected_command: Option<u8>) -> usize {
    usize::from(
        expected_command
            .is_some_and(|id| response.len() >= 2 && response[0] == 0 && response[1] == id),
    )
}

#[derive(Clone, Copy, Default)]
pub(crate) struct ReportSizes {
    pub(crate) input: usize,
    pub(crate) output: usize,
    pub(crate) feature: usize,
}

pub(crate) fn report_sizes(descriptor: &[u8]) -> ReportSizes {
    #[derive(Clone, Copy, Default)]
    struct Global {
        report_size: u32,
        report_count: u32,
        report_id: u8,
    }
    let mut global = Global::default();
    let mut stack = Vec::new();
    let mut input_bits = [0_u32; 256];
    let mut output_bits = [0_u32; 256];
    let mut feature_bits = [0_u32; 256];
    let mut index = 0;
    while index < descriptor.len() {
        let prefix = descriptor[index];
        index += 1;
        if prefix == 0xfe {
            if index + 2 > descriptor.len() {
                break;
            }
            let size = usize::from(descriptor[index]);
            index = index.saturating_add(2 + size);
            continue;
        }
        let size = match prefix & 0x03 {
            0 => 0,
            1 => 1,
            2 => 2,
            _ => 4,
        };
        if index + size > descriptor.len() {
            break;
        }
        let value = descriptor[index..index + size]
            .iter()
            .enumerate()
            .fold(0_u32, |value, (shift, byte)| {
                value | (u32::from(*byte) << (shift * 8))
            });
        index += size;
        let item_type = (prefix >> 2) & 0x03;
        let tag = prefix >> 4;
        match (item_type, tag) {
            (1, 7) => global.report_size = value,
            (1, 8) => global.report_id = value as u8,
            (1, 9) => global.report_count = value,
            (1, 10) => stack.push(global),
            (1, 11) => {
                if let Some(previous) = stack.pop() {
                    global = previous;
                }
            }
            (0, 8) => {
                input_bits[usize::from(global.report_id)] +=
                    global.report_size * global.report_count;
            }
            (0, 9) => {
                output_bits[usize::from(global.report_id)] +=
                    global.report_size * global.report_count;
            }
            (0, 11) => {
                feature_bits[usize::from(global.report_id)] +=
                    global.report_size * global.report_count;
            }
            _ => {}
        }
    }
    let bytes = |values: &[u32; 256]| {
        values
            .iter()
            .copied()
            .max()
            .filter(|bits| *bits != 0)
            .map_or(0, |bits| (bits as usize).div_ceil(8) + 1)
    };
    ReportSizes {
        input: bytes(&input_bits),
        output: bytes(&output_bits),
        feature: bytes(&feature_bits),
    }
}

fn prepare_report(
    report: &mut Vec<u8>,
    command: &[u8],
    offset: usize,
    length: usize,
    kind: &str,
) -> Result<()> {
    if command.len().saturating_add(offset) > length {
        return Err(OpenSeriesError::Protocol(format!(
            "{kind} report length {length} cannot carry this command."
        )));
    }
    report.resize(length, 0);
    report.fill(0);
    report[offset..offset + command.len()].copy_from_slice(command);
    Ok(())
}

pub(crate) fn map_hid_error(error: HidError) -> OpenSeriesError {
    if let HidError::IoError { error: io } = &error {
        return match io.kind() {
            ErrorKind::PermissionDenied => OpenSeriesError::PermissionDenied,
            ErrorKind::TimedOut | ErrorKind::WouldBlock => OpenSeriesError::Timeout,
            ErrorKind::NotConnected
            | ErrorKind::BrokenPipe
            | ErrorKind::ConnectionAborted
            | ErrorKind::ConnectionReset
            | ErrorKind::UnexpectedEof => OpenSeriesError::Disconnected,
            _ => OpenSeriesError::Hid(error),
        };
    }
    let message = error.to_string();
    let lower = message.to_lowercase();
    if lower.contains("permission") || lower.contains("access denied") {
        OpenSeriesError::PermissionDenied
    } else if lower.contains("timeout") {
        OpenSeriesError::Timeout
    } else if lower.contains("no such file")
        || lower.contains("not found")
        || lower.contains("disconnected")
    {
        OpenSeriesError::Disconnected
    } else {
        OpenSeriesError::Hid(error)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn prepare_report_reuses_and_clears_the_buffer() {
        let mut report = vec![0xff; 8];

        prepare_report(&mut report, &[0x10, 0x20], 1, 4, "Output").unwrap();
        assert_eq!(report, [0, 0x10, 0x20, 0]);

        prepare_report(&mut report, &[0x30], 2, 6, "Output").unwrap();
        assert_eq!(report, [0, 0, 0x30, 0, 0, 0]);
    }

    #[test]
    fn prepare_report_rejects_an_oversized_command() {
        let mut report = Vec::new();
        let error = prepare_report(&mut report, &[1, 2, 3], 2, 4, "Feature").unwrap_err();

        assert!(matches!(error, OpenSeriesError::Protocol(_)));
        assert!(report.is_empty());
    }

    #[test]
    fn response_normalization_uses_a_slice_offset() {
        assert_eq!(normalized_response_start(&[0, 0xb0, 1], Some(0xb0)), 1);
        assert_eq!(normalized_response_start(&[0xb0, 1], Some(0xb0)), 0);
        assert_eq!(normalized_response_start(&[0, 0xb0, 1], None), 0);
    }
}
