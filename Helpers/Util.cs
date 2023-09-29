using Lab.Server.Models;
using System.IO.Compression;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Lab.Server.Helpers
{
    public static partial class Util
    {
        static readonly byte[] crc8Table = new byte[]
        {
            
        };

        static readonly UInt16[] crc16Table = new UInt16[]
        {
            
        };


        public static byte FastCRC8(byte[] Buffer, int length)
        {
            byte crc = 0;
            for (var i = 0; i < length; ++i)
            {
                crc = crc8Table[crc ^ Buffer[i]];
            }
            return crc;
        }

        public static UInt16 FastCRC16(ref ReadOnlySpan<byte> Buffer, int length)
        {
            UInt16 crc = 0;
            UInt16 x;


            for (int i = 0; i < length; ++i)
            {
                x = (UInt16)(crc ^ Buffer[i]);
                crc = (UInt16)((crc >> 8) ^ crc16Table[x & 0x00FF]);
            }
            return crc;
        }

        public static byte CheckSum8_2s_Complement(byte[] Buffer, int length)
        {
            UInt16 checksum = 0;


            for (int i = 0; i < length; ++i)
            {
                checksum += Buffer[i];
                checksum &= 0xFF;
            }

            return (byte)(0x100 - checksum);
        }


        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }
    }
}
