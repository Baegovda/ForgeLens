// R76: raw BC6H mip -> Radiance .hdr (float). usage: bc6hdr <in> <w> <h> <out.hdr> [u|s] [scale]
using System; using System.IO; using System.Text; using BCnEncoder.Decoder; using BCnEncoder.Shared;
var data = File.ReadAllBytes(args[0]); int w = int.Parse(args[1]), h = int.Parse(args[2]);
var fmt = args.Length > 4 && args[4] == "s" ? CompressionFormat.Bc6S : CompressionFormat.Bc6U;
float sc = args.Length > 5 ? float.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture) : 1f;
var px = new BcDecoder().DecodeRawHdr(data, w, h, fmt);
using var f = File.Create(args[3]);
var hdr = Encoding.ASCII.GetBytes($"#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y {h} +X {w}\n"); f.Write(hdr);
double mx = 0, sum = 0; var row = new byte[w * 4];
for (int y = 0; y < h; y++) { for (int x = 0; x < w; x++) { var c = px[y * w + x]; float r = c.r * sc, g = c.g * sc, b = c.b * sc;
  float m = Math.Max(r, Math.Max(g, b)); mx = Math.Max(mx, m); sum += 0.2126 * r + 0.7152 * g + 0.0722 * b;
  if (m < 1e-32f) { row[x*4]=row[x*4+1]=row[x*4+2]=row[x*4+3]=0; continue; }
  int e = (int)Math.Ceiling(Math.Log2(m)); float k = (float)Math.Pow(2, -e) * 255.99f; if (m * k > 255.99f) { e++; k /= 2; }
  row[x*4]=(byte)(r*k); row[x*4+1]=(byte)(g*k); row[x*4+2]=(byte)(b*k); row[x*4+3]=(byte)(e+128); } f.Write(row); }
Console.WriteLine($"bc6hdr {w}x{h} {fmt} max={mx:F3} meanLum={sum/(w*(double)h):F4} -> {args[3]}");
