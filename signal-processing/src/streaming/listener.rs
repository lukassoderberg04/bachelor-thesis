use std::{
    error::Error,
    io,
    net::{ToSocketAddrs, UdpSocket},
};

pub struct StokesUdpListener {
    socket: UdpSocket,
}

impl StokesUdpListener {
    /// Binds to an address and returns an instance of a bound listener.
    ///
    /// ## Parameters
    ///
    /// - `addr` Address to bind to
    ///
    /// ## Returns
    ///
    /// A `Result` resolving to an instance of `Self` bound to `addr`.
    pub fn bind<T: ToSocketAddrs>(addr: T) -> io::Result<Self> {
        let socket = UdpSocket::bind(addr)?;

        Ok(Self { socket })
    }

    /// Recieves a value from the stokes UDP stream.
    ///
    /// ## Returns
    ///
    /// Returns a `Result` resolving to a tuple containing, in order:
    /// timestamps, S0, S1, S2 and S3. The function returns an error if
    /// either there is no message currently pending, or if
    pub fn recv(&self) -> Result<(u32, f64, f64, f64, f64), String> {
        let mut buf = [0u8; 24];
        let (amt, _src) = self
            .socket
            .recv_from(&mut buf)
            .map_err(|_| "Didn't recieve data.".to_string())?;

        // Stokes vectors: 5 * float32 + 1 UInt32 (S0, S1, S2, S3, DOP, TIME) aka 24 bytes.
        if amt != 24 {
            return Err(format!("Incorrect byte amount. Expected 24 bytes, recieved {}.", amt));
        }

        let (t, s0, s1, s2, s3, _dop) = Self::deserialize(&buf);

        Ok((t, s0, s1, s2, s3))
    }

    /// Deserialises data recieved from the stokes stream.
    ///
    /// ## Parameters
    ///
    /// - `buf` Buffer recieved from UDP stream. The slice should have a length of 24 bytes.
    ///
    /// ## Returns
    ///
    /// A tuple of deserialized data. In order, the values are:
    /// timestamps, S0, S1, S2, S3 and DOP.
    fn deserialize(buf: &[u8]) -> (u32, f64, f64, f64, f64, f64) {
        // Can safely call unwrap as buffer size is predetermined.
        let s0_bytes: [u8; 4] = buf[0..4].try_into().unwrap();
        let s1_bytes: [u8; 4] = buf[4..8].try_into().unwrap();
        let s2_bytes: [u8; 4] = buf[8..12].try_into().unwrap();
        let s3_bytes: [u8; 4] = buf[12..16].try_into().unwrap();
        let dop_bytes: [u8; 4] = buf[16..20].try_into().unwrap();
        let t_bytes: [u8; 4] = buf[20..24].try_into().unwrap();

        // Read bytes as f32 in little endian and cast to f64 for numerical stability during computations
        let s0 = f32::from_le_bytes(s0_bytes) as f64;
        let s1 = f32::from_le_bytes(s1_bytes) as f64;
        let s2 = f32::from_le_bytes(s2_bytes) as f64;
        let s3 = f32::from_le_bytes(s3_bytes) as f64;
        let dop = f32::from_le_bytes(dop_bytes) as f64;
        let t = u32::from_le_bytes(t_bytes);

        (t, s0, s1, s2, s3, dop)
    }
}
