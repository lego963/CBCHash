using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace CBCHash
{
    internal static class Program
    {
        private static void Main()
        {
            const ulong iv = 3926821066414368105;
            const ulong key64 = 5931337798882863083;
            Console.WriteLine($"key64: {key64}");
            Console.WriteLine($"IV: {iv}");
            for (var i = 0; i < 20; i++)
            {
                var rnd = new Random();
                var tmpMessage = rnd.Next(1000000, int.MaxValue);
                Console.WriteLine($"Message: {tmpMessage}");
                WorkIt(tmpMessage.ToString());
                Console.WriteLine(new string('-', 25));
            }

            Console.ReadKey();
        }
        private static void WorkIt(string input, ulong iv = 3926821066414368105, ulong key64 = 5931337798882863083)
        {
            var message = Padding(input);
            var mesgC = ToBlocks(message);

            for (var i = 0; i < mesgC.Length; i++)
            {
                mesgC[i] = Encrypt(i == 0 ? mesgC[i] ^ iv : mesgC[i] ^ mesgC[i - 1], key64);
            }
            EvaluateBlock(mesgC[mesgC.Length - 1]);
        }
        [SuppressMessage("ReSharper", "PossibleLossOfFraction")]
        private static void EvaluateBlock(ulong mesg) //Метод, переводящий строку с двоичными данными в символьный формат.
        {

            var destroyMsg = new ushort[4];

            destroyMsg[0] = (ushort)(mesg >> 3 * 16);
            destroyMsg[1] = (ushort)(((mesg >> 2 * 16) << 3 * 16) >> 3 * 16);
            destroyMsg[2] = (ushort)((mesg << 2 * 16) >> 3 * 16);
            destroyMsg[3] = (ushort)((mesg << 3 * 16) >> 3 * 16);

            var symbols = new ulong[4];
            for (var i = 0; i < 4; i++)
            {
                symbols[i] = destroyMsg[i];
            }


            var data = BitConverter.GetBytes(mesg);
            Array.Reverse(data);
            var hex = BitConverter.ToString(data).Replace('-', ' ');
            //корреляция
            uint mask = 1;
            var x = mesg;
            var k = 0; //кол-во 1 в ulong
            while (x != 0)
            {
                if ((x & mask) == 1)
                    k++;
                x >>= 1;
            }
            var correlation = (double)k / 64; //корреляция и мат. ожидание в одном
            //дисперсия
            var dispersion = 0D;
            x = mesg;
            for (var i = 0; i < 64; i++)
            {
                double bit = x & mask;
                dispersion += (bit - correlation) * (bit - correlation);
                x >>= 1;
            }
            dispersion /= 64;
            //xi^2

            var v = new int[8];
            x = mesg;
            mask = 15;
            for (var i = 0; i < 16; i++)
            {
                var y = x & mask;
                v[(int)(y / 2)]++;
                x >>= 4;
            }

            var xi = v.Sum(item => ((double)item - 16 / v.Length) * ((double)item - 16 / v.Length));

            Console.WriteLine($"Unicode: {MessageToHex(symbols)}");
            Console.WriteLine($"16bit: {hex}");
            Console.WriteLine($"Correlation: {correlation}");
            Console.WriteLine($"Dispersion: {dispersion}");
            Console.WriteLine("Frequencies: ");
            for (var i = 0; i < v.Length; i++)
                Console.WriteLine($"{2 * i }, {2 * i + 1}: {v[i]}");
            Console.WriteLine($"Xi^2: {xi}");
        }
        private static string MessageToHex(IEnumerable<ulong> msg)
        {
            var result = string.Empty;
            var tmp = new ushort[4];
            foreach (var item in msg)
            {
                tmp[0] = (ushort)(item >> 3 * 16);
                tmp[1] = (ushort)(item >> 2 * 16 << 3 * 16 >> 3 * 16);
                tmp[2] = (ushort)(item << 2 * 16 >> 3 * 16);
                tmp[3] = (ushort)(item << 3 * 16 >> 3 * 16);
                result = tmp.Aggregate(result, (current, element) => current + element.ToString("X"));
            }
            return result;
        }
        private static ushort CycleMoveLeft(ushort number, byte offset)
        {
            return (ushort)((number << offset) | (number >> sizeof(ushort) * 8 - offset));
        }
        private static ulong CycleMoveRight(ulong number, int offset) //offset - количество, которое надо сдвинуть
        {
            return (number >> offset) | (number << sizeof(ulong) * 8 - offset);
        }
        private static ushort CycleMoveRight(ushort number, byte offset)
        {
            return (ushort)((number >> offset) | (number << sizeof(ushort) * 8 - offset));
        }
        private static ushort F(ushort x, ushort y)
        {
            return (ushort)((~x + CycleMoveRight(y, (byte)2)) ^ CycleMoveLeft(y, 5));
        }
        private static ushort KeyGenerator(int i, ulong k)
        {
            return (ushort)((CycleMoveRight(k, i * 5) << 48) >> 48);
        }

        private static ulong Encrypt(ulong mesg, ulong key, int rounds = 16)
        {
            #region unpack mesg
            var i = 0;
            var temp = new ushort[4];
            temp[0] = (ushort)(mesg >> 3 * 16);
            temp[1] = (ushort)(((mesg >> 2 * 16) << 3 * 16) >> 3 * 16);
            temp[2] = (ushort)((mesg << 2 * 16) >> 3 * 16);
            temp[3] = (ushort)((mesg << 3 * 16) >> 3 * 16);
            #endregion

            while (i < rounds)
            {
                var key16 = KeyGenerator(i, key);
                ushort[] c = new ushort[temp.Length];
                c[0] = (ushort)(F(temp[0], temp[1]) ^ temp[3]);
                c[2] = temp[0];
                c[1] = temp[2];
                c[3] = (ushort)(key16 ^ temp[1]);
                temp = c;
                i++;
            }

            #region pack mesg
            ulong t1 = temp[0];
            t1 = t1 << 3 * 16;
            ulong t2 = temp[1];
            t2 = t2 << 2 * 16;
            ulong t3 = temp[2];
            t3 = t3 << 16;
            ulong t4 = temp[3];
            return t1 | t2 | t3 | t4;
            #endregion
        }

        private static string Padding(string s)
        {
            var n = s.Length * 16 % 64;
            if (n == 0) return s;
            var sb = new StringBuilder(s);
            sb.Append(new char(), (64 - n) / 16);
            return sb.ToString();
        }
        private static ulong[] ToBlocks(string s)
        {
            ushort[] temp = new ushort[4];
            ulong[] result = new ulong[s.Length / 4];
            for (int i = 0, j = 0; i < s.Length; i += 4, j++)
            {
                temp[0] = Convert.ToUInt16(s[i]);
                temp[1] = Convert.ToUInt16(s[i + 1]);
                temp[2] = Convert.ToUInt16(s[i + 2]);
                temp[3] = Convert.ToUInt16(s[i + 3]);
                #region pack mesg

                ulong t1 = temp[0];
                t1 = t1 << 3 * 16;
                ulong t2 = temp[1];
                t2 = t2 << 2 * 16;
                ulong t3 = temp[2];
                t3 = t3 << 16;
                ulong t4 = temp[3];
                result[j] = t1 | t2 | t3 | t4;
                #endregion

            }
            return result;
        }
    }
}