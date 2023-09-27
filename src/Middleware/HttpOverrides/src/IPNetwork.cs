﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Sockets;

namespace Microsoft.AspNetCore.HttpOverrides
{
    public class IPNetwork
    {
        public IPNetwork(IPAddress prefix, int prefixLength)
        {
            CheckPrefixLengthRange(prefix, prefixLength);
            Prefix = prefix;
            PrefixLength = prefixLength;
            PrefixBytes = Prefix.GetAddressBytes();
            Mask = CreateMask();
        }

        public IPAddress Prefix { get; }

        private byte[] PrefixBytes { get; }

        /// <summary>
        /// The CIDR notation of the subnet mask
        /// </summary>
        public int PrefixLength { get; }

        private byte[] Mask { get; }

        public bool Contains(IPAddress address)
        {
            if (Prefix.AddressFamily != address.AddressFamily)
            {
                return false;
            }

            var addressBytes = address.GetAddressBytes();
            for (int i = 0; i < PrefixBytes.Length && Mask[i] != 0; i++)
            {
                if ((PrefixBytes[i] & Mask[i]) != (addressBytes[i] & Mask[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private byte[] CreateMask()
        {
            var mask = new byte[PrefixBytes.Length];
            int remainingBits = PrefixLength;
            int i = 0;
            while (remainingBits >= 8)
            {
                mask[i] = 0xFF;
                i++;
                remainingBits -= 8;
            }
            if (remainingBits > 0)
            {
                mask[i] = (byte)(0xFF << (8 - remainingBits));
            }

            return mask;
        }

        private static void CheckPrefixLengthRange(IPAddress prefix, int prefixLength)
        {
            if (prefixLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(prefixLength));
            }

            if (prefix.AddressFamily == AddressFamily.InterNetwork && prefixLength > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(prefixLength));
            }

            if (prefix.AddressFamily == AddressFamily.InterNetworkV6 && prefixLength > 128)
            {
                throw new ArgumentOutOfRangeException(nameof(prefixLength));
            }
        }
    }
}
