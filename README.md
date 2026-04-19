# ClipboardCompressor

A lightweight Windows tray app that compresses images on your clipboard before you paste them. Built mainly to get around Discord's 10MB upload limit without having to manually export and resize screenshots.

## What it does

Whenever you copy an image, the app intercepts it and replaces it with a compressed version using palette quantization (similar to how TinyPNG works). The result looks nearly identical but is usually 60-80% smaller. When you paste into Discord or any other app, it uses the compressed version automatically.

## Features

- Auto mode: compresses every image the moment you copy it
- Manual mode: asks you on each Ctrl+V whether to paste compressed or original
- PNG output using 256-color palette quantization for near-lossless results
- JPEG mode with adjustable quality if you want even smaller files
- Transparency support for PNG images
- Tray notifications showing before/after file sizes
- Start with Windows option

## Requirements

- Windows 10 or 11
- .NET 10 runtime

## Building

```
dotnet build -c Release
```

The output is a single exe in `bin/Release/net10.0-windows/`.

## How it works

For PNG mode, the app runs a median-cut color quantization algorithm to reduce the image to a 256-color palette. It builds a 6-bit-per-channel lookup table (64x64x64 = 262,144 entries) for fast per-pixel mapping, then saves the result as an indexed 8bpp PNG. This cuts file size dramatically because you go from 4 bytes per pixel down to 1, and DEFLATE compresses a palette image much better than full color.

For JPEG mode, it encodes to JPEG first (stripping high-frequency noise), then runs the same palette quantization on the result before saving as PNG.

The app writes both a standard CF_DIB bitmap format and a raw "PNG" clipboard format via the Win32 API directly. The raw write is necessary because WinForms wraps clipboard data in BinaryFormatter, which Electron-based apps like Discord cannot read.
