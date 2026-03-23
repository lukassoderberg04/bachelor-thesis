use std::net::{SocketAddr, ToSocketAddrs, UdpSocket};

pub struct AudioUdpSender {
    socket: UdpSocket,
    target: SocketAddr,
}

impl AudioUdpSender {
    pub fn bind<T: ToSocketAddrs, V: ToSocketAddrs>(addr: T, target: V) -> Result<Self, String> {
        let socket = UdpSocket::bind(addr).map_err(|err| err.to_string())?;
        let target = target
            .to_socket_addrs()
            .map_err(|err| err.to_string())?
            .next()
            .ok_or("No valid target address.")?;

        Ok(Self { socket, target })
    }

    pub fn send(&self, amplitude: f64, timestamp: u32) -> Result<(), String> {
        let mut buf = Vec::new();

        buf.extend_from_slice(&timestamp.to_le_bytes());
        buf.extend_from_slice(&(amplitude as f32).to_le_bytes());

        self.socket.send_to(&buf, self.target).map_err(|err| err.to_string())?;

        Ok(())
    }
}
