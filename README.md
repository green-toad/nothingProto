# nothingProto

An experimental data transfer protocol - `Cat` - that lives on top of TCP/IP.  
  
It started as a test bed for a VPN/proxy-style tunnel, and the interesting part is the secure channel underneath: a TLS-like handshake, X25519 key exchange, and a custom symmetric cipher: RE5 (Jabr)

## What it does

nothingProto opens a regular TCP connection and then builds its own session on top of it. Before any payload is sent, the client and the server go through a handshake modeled after the TLS one: both sides agree on session parameters, exchange public keys, and derive a shared secret. Everything after the handshake is encrypted with that secret.

The pieces:

- **Handshake.** Follows the general shape of a TLS handshake (hello, key exchange, then switching to encrypted traffic), but it is its own implementation with its own message format.  
    **It is not compatible with regular TLS**
- **Key exchange.** X25519 (Curve25519 Diffie-Hellman).  
    Each side generates an ephemeral key pair, the public halves are exchanged over the open connection, and both sides compute the same shared secret without it ever being sent.
- **Symmetric encryption.** Once the shared secret exists, traffic is encrypted with Jabr.RE5  
    (if you want you can [learn more about Jabr](https://github.com/Gyroscopic-why/Jabr))
- **Message format.** All packets, both during the handshake and after it, use a single framing format defined in `MessageFormat.cs`.

## Repository layout

The server and the client live in the same project and are built into a single console executable. When you start it, the program runs either as the server or as the client, depending on how you launch it.

- `Program.cs` - entry point, picks the mode
- `Server/` - server-side logic
- `Client/` - client-side logic
- `lib/` - dependencies and shared code used by both sides
- `MessageFormat.cs` - packet framing and message types
- `AsymetrycCryptographyLauncher.cs` - runs the asymmetric part of the handshake (X25519)
- `SymetricCryptoPlug.cs` - the point where the symmetric cipher is plugged in
- `nothingProto.csproj`, `nothingProto.slnx` - project and solution files

## Building (example for Linux-x64)

You need a recent .NET SDK (>= 10.0)

```
git clone https://github.com/green-toad/nothingProto.git
cd nothingProto
dotnet restore
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

Run the resulting executable once on one machine (or terminal) in server mode, and once more in client mode to connect to it. Running both on localhost is the easiest way to try it.
