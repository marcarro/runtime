// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

// A pair of opposite shifts with the same constant count is a mask:
//   (x >> n) << n   clears the low n bits (with either kind of right shift)
//   (x << n) >>> n  clears the high n bits (with a logical right shift only)
// These tests check the instructions. A wrong result would be caught by any test,
// but a rule that silently stops firing wouldn't.
namespace TestShiftPairToMask
{
    public class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Fact]
        public static int CheckShiftPairToMask()
        {
            bool fail = false;

            if (ClearLow5_UInt32(0x12345678u) != 0x12345660u)
            {
                fail = true;
            }

            if (ClearLow5_Int32(-1) != -32)
            {
                fail = true;
            }

            if (KeepLow24_UInt32(0x12345678u) != 0x00345678u)
            {
                fail = true;
            }

            // The outer shift is arithmetic, so this isn't a mask.
            if (ArithmeticOuterShift_Int32(0x00800000) != unchecked((int)0xFF800000))
            {
                fail = true;
            }

            if (ClearLow8_UInt64(0x123456789ABCDEF0ul) != 0x123456789ABCDE00ul)
            {
                fail = true;
            }

            if (KeepLow32_UInt64(0x123456789ABCDEF0ul) != 0x9ABCDEF0ul)
            {
                fail = true;
            }

            if (KeepLow24_UInt64(0x123456789ABCDEF0ul) != 0xBCDEF0ul)
            {
                fail = true;
            }

            uint[] table = new uint[8];
            table[3] = 7;
            if (SharedInnerShift(table, 0x305u) != 0x307u)
            {
                fail = true;
            }

            if (fail)
            {
                return 101;
            }
            return 100;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static uint ClearLow5_UInt32(uint x)
        {
            // X64-NOT: shr
            // X64-NOT: shl
            // X64: and

            // ARM64-NOT: lsr
            // ARM64-NOT: lsl
            // ARM64-FULL-LINE: and {{w[0-9]+}}, {{w[0-9]+}}, #{{.*}}
            return (x >> 5) << 5;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int ClearLow5_Int32(int x)
        {
            // The inner right shift is arithmetic here, which doesn't matter because
            // the outer shift discards the bits it fills.

            // X64-NOT: sar
            // X64-NOT: shl
            // X64: and

            // ARM64-NOT: asr
            // ARM64-NOT: lsl
            // ARM64-FULL-LINE: and {{w[0-9]+}}, {{w[0-9]+}}, #{{.*}}
            return (x >> 5) << 5;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static uint KeepLow24_UInt32(uint x)
        {
            // X64-NOT: shl
            // X64-NOT: shr
            // X64: and

            // ARM64-NOT: lsl
            // ARM64-NOT: lsr
            // ARM64-FULL-LINE: and {{w[0-9]+}}, {{w[0-9]+}}, #{{.*}}
            return (x << 8) >>> 8;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int ArithmeticOuterShift_Int32(int x)
        {
            // The outer shift copies the sign bit, so this must stay shifts (or an equivalent
            // sign extension) rather than become an 'and'.

            // X64-NOT: and
            // ARM64-NOT: and
            return (x << 8) >> 8;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static ulong ClearLow8_UInt64(ulong x)
        {
            // X64-NOT: shr
            // X64-NOT: shl
            // X64: and

            // ARM64-NOT: lsr
            // ARM64-NOT: lsl
            // ARM64-FULL-LINE: and {{x[0-9]+}}, {{x[0-9]+}}, #-256
            return (x >> 8) << 8;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static ulong KeepLow32_UInt64(ulong x)
        {
            // The mask 0xFFFFFFFF doesn't fit the sign-extended imm32 of an x64 'and'. Loading it
            // costs more than the two shifts, so x64 must not use 'and' here.
            // X64-NOT: and

            // arm64 can encode 0xFFFFFFFF directly in the 'and'.
            // ARM64-NOT: lsl
            // ARM64-NOT: lsr
            // ARM64-FULL-LINE: and {{x[0-9]+}}, {{x[0-9]+}}, #{{.*}}
            return (x << 32) >>> 32;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static ulong KeepLow24_UInt64(ulong x)
        {
            // The mask 0xFFFFFF fits the immediate of an x64 'and', so unlike KeepLow32_UInt64
            // this is rewritten on x64 too.

            // X64-NOT: shl
            // X64-NOT: shr
            // X64: and

            // ARM64-NOT: lsl
            // ARM64-NOT: lsr
            // ARM64-FULL-LINE: and {{x[0-9]+}}, {{x[0-9]+}}, #{{.*}}
            return (x << 40) >>> 40;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static uint SharedInnerShift(uint[] table, uint x)
        {
            // 'x >> 8' is the index as well as the inner shift of the pair, so it's computed once,
            // then shared. Rewriting the pair to 'and x, -256' would give up the sharing and cost
            // an extra 'mov' on x64. Keep the pair as it is.

            // X64: shr
            // X64-NOT: and

            // ARM64: lsr
            return table[x >> 8] + ((x >> 8) << 8);
        }
    }
}
