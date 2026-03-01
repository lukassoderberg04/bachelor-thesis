// TODO: Eventually look over this for any discreapancies with the rest of the signal analyzer

use std::net::UdpSocket;

/// Sends processed audio samples to the visualizer over UDP on port 5001.
///
/// PACKET FORMAT (little-endian) — must stay in sync with
/// `PacketDeserializer.TryDeserializeAudio` in `pm1000-visualizer`:
///
///   Header – 10 bytes:
///     u32  sequence_nr    – wraps at 2^32, used to detect dropped packets
///     u32  sample_rate_hz – e.g. 16000
///     u16  block_size     – number of f32 samples that follow
///
///   Payload – 4 bytes × block_size:
///     f32  amplitude      – one audio sample, typically −1.0 … +1.0
///
/// Usage:
///   let mut sender = AudioUdpSender::new("127.0.0.1", 16000)?;
///   sender.send_block(&samples)?;
pub struct AudioUdpSender {
    socket: UdpSocket,
    target: String,
    sample_rate_hz: u32,
    sequence_nr: u32,
}

impl AudioUdpSender {
    pub const AUDIO_PORT: u16 = 5001;
    pub const HEADER_SIZE: usize = 10; // 4 + 4 + 2 bytes

    /// Creates a new sender. `target_ip` is the IP of the machine running the visualizer.
    pub fn new(target_ip: &str, sample_rate_hz: u32) -> std::io::Result<Self> {
        let socket = UdpSocket::bind("0.0.0.0:0")?; // OS picks an ephemeral source port
        Ok(Self {
            socket,
            target: format!("{}:{}", target_ip, Self::AUDIO_PORT),
            sample_rate_hz,
            sequence_nr: 0,
        })
    }

    /// Serializes `samples` into the wire format and sends one UDP packet.
    /// Returns the number of bytes sent, or an IO error.
    pub fn send_block(&mut self, samples: &[f32]) -> std::io::Result<usize> {
        let payload = self.serialize(samples);
        let sent = self.socket.send_to(&payload, &self.target)?;
        self.sequence_nr = self.sequence_nr.wrapping_add(1);
        Ok(sent)
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    fn serialize(&self, samples: &[f32]) -> Vec<u8> {
        let mut buf = Vec::with_capacity(Self::HEADER_SIZE + samples.len() * 4);

        buf.extend_from_slice(&self.sequence_nr.to_le_bytes());
        buf.extend_from_slice(&self.sample_rate_hz.to_le_bytes());
        buf.extend_from_slice(&(samples.len() as u16).to_le_bytes());

        for &s in samples {
            buf.extend_from_slice(&s.to_le_bytes());
        }

        buf
    }
}