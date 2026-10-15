using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class MakeIcon
{
    static void Main(string[] args)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        List<Bitmap> bmps = new List<Bitmap>();
        foreach (int s in sizes) bmps.Add(DrawTomato(s));
        byte[] ico = BuildIco(bmps);
        File.WriteAllBytes(args[0], ico);
        foreach (Bitmap b in bmps) b.Dispose();
    }

    // 按 64x64 设计稿等比绘制番茄
    static Bitmap DrawTomato(int s)
    {
        Bitmap bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float u = s / 64f;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(231, 76, 60)))
                g.FillEllipse(b, 6 * u, 14 * u, 52 * u, 44 * u);
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(255, 122, 102)))
                g.FillEllipse(hb, 16 * u, 22 * u, 14 * u, 10 * u);
            using (SolidBrush grn = new SolidBrush(Color.FromArgb(46, 160, 90)))
            {
                g.FillEllipse(grn, 24 * u, 5 * u, 16 * u, 13 * u);
                g.FillEllipse(grn, 12 * u, 11 * u, 12 * u, 9 * u);
                g.FillEllipse(grn, 40 * u, 11 * u, 12 * u, 9 * u);
            }
        }
        return bmp;
    }

    // 把多张 Bitmap 打包成多尺寸 ICO
    static byte[] BuildIco(List<Bitmap> bmps)
    {
        int count = bmps.Count;
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(ms))
        {
            w.Write((short)0); // reserved
            w.Write((short)1); // type: icon
            w.Write((short)count);

            int offset = 6 + 16 * count;
            List<byte[]> images = new List<byte[]>();
            foreach (Bitmap b in bmps)
            {
                byte[] img = BmpToIconImage(b);
                images.Add(img);
                int sz = b.Width;
                w.Write((byte)(sz >= 256 ? 0 : sz)); // width
                w.Write((byte)(sz >= 256 ? 0 : sz)); // height
                w.Write((byte)0);  // palette
                w.Write((byte)0);  // reserved
                w.Write((short)1); // planes
                w.Write((short)32);// bit count
                w.Write(img.Length);
                w.Write(offset);
                offset += img.Length;
            }
            foreach (byte[] img in images) w.Write(img);
            w.Flush();
            return ms.ToArray();
        }
    }

    // 单张 Bitmap -> ICO 图像（BITMAPINFOHEADER + BGRA 自下而上 + AND 掩码）
    static byte[] BmpToIconImage(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        int xorSize = w * h * 4;
        int andStride = ((w + 31) / 32) * 4;
        int andSize = andStride * h;
        byte[] data = new byte[40 + xorSize + andSize];

        using (MemoryStream ms = new MemoryStream(data))
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(40);          // biSize
            bw.Write(w);           // biWidth
            bw.Write(h * 2);       // biHeight（XOR + AND）
            bw.Write((short)1);    // planes
            bw.Write((short)32);   // bpp
            bw.Write(0);           // compression
            bw.Write(xorSize + andSize);
            bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
        }

        int p = 40;
        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = bmp.GetPixel(x, y);
                data[p++] = c.B;
                data[p++] = c.G;
                data[p++] = c.R;
                data[p++] = c.A;
            }
        }
        // AND 掩码保持全 0（32bpp 图标用 alpha 通道，AND 掩码忽略）
        return data;
    }
}
