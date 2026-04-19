using System.Text;

namespace VSRO_CONTROL_API.VSRO.Patching
{
    /// <summary>
    /// Binary patcher for vSRO 1.188 SR_GameServer.exe.
    /// All offsets from the reflected SR_Patcher are raw file offsets (decimal).
    /// Object limit offsets are virtual addresses converted at apply time using PE header data.
    /// A backup is created before every patch attempt.
    /// </summary>
    public class GameServerPatcher
    {
        private readonly string _exePath;
        private readonly string _backupDir;
        private readonly List<PatchOperation> _pendingPatches = new();

        public GameServerPatcher(string exePath)
        {
            if (!File.Exists(exePath))
                throw new FileNotFoundException($"SR_GameServer.exe not found at: {exePath}");

            _exePath = exePath;
            _backupDir = Path.Combine(Path.GetDirectoryName(exePath)!, "patch_backups");
        }

        /// <summary>
        /// Changes the maximum player level (1-254).
        /// Patches two offsets that enforce the level cap.
        /// </summary>
        public GameServerPatcher SetMaxLevel(byte level)
        {
            if (level < 1)
                throw new ArgumentOutOfRangeException(nameof(level), "Level must be between 1 and 254.");

            _pendingPatches.Add(new PatchOperation("MaxLevel[0]", 877598, new[] { level }));
            _pendingPatches.Add(new PatchOperation("MaxLevel[1]", 938697, new[] { level }));
            return this;
        }

        /// <summary>
        /// Changes the mastery limit (1-900).
        /// Patches the primary mastery cap and a secondary reference.
        /// </summary>
        public GameServerPatcher SetMasteryLimit(short limit)
        {
            if (limit < 1 || limit > 900)
                throw new ArgumentOutOfRangeException(nameof(limit), "Mastery limit must be between 1 and 900.");

            byte[] bytes = BitConverter.GetBytes(limit);
            _pendingPatches.Add(new PatchOperation("MasteryLimit", 1689063, bytes));
            _pendingPatches.Add(new PatchOperation("MasteryLimit[secondary]", 939961, new[] { bytes[0] }));
            return this;
        }

        /// <summary>
        /// Sets the pet level cap byte.
        /// Default patched value is 0xB6 (182).
        /// </summary>
        public GameServerPatcher SetPetLevelCap(byte value = 0xB6)
        {
            _pendingPatches.Add(new PatchOperation("PetLevelCap", 939125, new[] { value }));
            return this;
        }

        /// <summary>
        /// Disables crash dump file generation.
        /// Replaces the dump call with a JMP + NOP.
        /// </summary>
        public GameServerPatcher DisableDumpFiles()
        {
            _pendingPatches.Add(new PatchOperation("DisableDumps[jmp]", 5652576, new byte[] { 0xE9, 0x98, 0x02, 0x00 }));
            _pendingPatches.Add(new PatchOperation("DisableDumps[nop]", 5652581, new byte[] { 0x90 }));
            return this;
        }

        /// <summary>
        /// Fixes rate calculation overflow by patching five offsets.
        /// Writes 0x42 to each location.
        /// </summary>
        public GameServerPatcher FixRates()
        {
            int[] offsets = { 160078, 160247, 160418, 160587, 160716 };
            for (int i = 0; i < offsets.Length; i++)
            {
                _pendingPatches.Add(new PatchOperation($"FixRates[{i}]", offsets[i], new byte[] { 0x42 }));
            }
            return this;
        }

        /// <summary>
        /// Disables the green/red/black book system.
        /// NOPs out the 12-byte check.
        /// </summary>
        public GameServerPatcher DisableGreenBook()
        {
            byte[] nops = new byte[12];
            Array.Fill(nops, (byte)0x90);
            _pendingPatches.Add(new PatchOperation("DisableGreenBook", 82747, nops));
            return this;
        }

        /// <summary>
        /// Spoofs the server's bind IP address.
        /// Patches the ASM redirect and writes the IP string to a fixed buffer.
        /// </summary>
        public GameServerPatcher SpoofIP(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ArgumentException("IP address cannot be empty.", nameof(ipAddress));

            byte[] ipBytes = Encoding.ASCII.GetBytes(ipAddress);
            if (ipBytes.Length > 32)
                throw new ArgumentException("IP address string is too long (max 32 characters).", nameof(ipAddress));

            byte[] asmRedirect = { 0x20, 0x8E, 0xAD };
            _pendingPatches.Add(new PatchOperation("SpoofIP[asm0]", 5465530, asmRedirect));
            _pendingPatches.Add(new PatchOperation("SpoofIP[asm1]", 5465614, asmRedirect));

            // Clear the 32-byte buffer and write the new IP (null-terminated)
            byte[] ipBuffer = new byte[32];
            Array.Copy(ipBytes, ipBuffer, ipBytes.Length);
            _pendingPatches.Add(new PatchOperation("SpoofIP[string]", 7179808, ipBuffer));

            return this;
        }

        /// <summary>
        /// Patches the object spawn limit (default 50,000, max 100,000).
        /// Modifies four count addresses and the CMP loop boundary.
        /// 
        /// These offsets use virtual addresses (as seen in OllyDbg) and are
        /// converted to file offsets at apply time using the PE section headers.
        /// 
        /// The method verifies the existing bytes at each offset match expected
        /// values before patching to prevent corrupting an incompatible binary.
        /// </summary>
        public GameServerPatcher SetObjectLimit(int limit)
        {
            if (limit < 50000 || limit > 100000)
                throw new ArgumentOutOfRangeException(nameof(limit), "Object limit must be between 50,000 and 100,000.");

            byte[] countBytes = BitConverter.GetBytes((uint)limit);
            uint cmpValue = (uint)((long)limit * 0x1D0);
            byte[] cmpBytes = BitConverter.GetBytes(cmpValue);

            // Default 50k values for verification
            byte[] default50kCount = BitConverter.GetBytes((uint)50000);     // 50 C3 00 00
            byte[] default50kCmp = BitConverter.GetBytes((uint)(50000 * 0x1D0)); // 00 01 62 01

            _pendingPatches.Add(new PatchOperation("ObjectLimit[count0]", 0x0054D60A, countBytes, isVirtualAddress: true));
            _pendingPatches.Add(new PatchOperation("ObjectLimit[count1]", 0x0054D61F, countBytes, isVirtualAddress: true));
            _pendingPatches.Add(new PatchOperation("ObjectLimit[count2]", 0x0054D655, countBytes, isVirtualAddress: true));
            _pendingPatches.Add(new PatchOperation("ObjectLimit[count3]", 0x0054D664, countBytes, isVirtualAddress: true));
            _pendingPatches.Add(new PatchOperation("ObjectLimit[cmp]", 0x0054D6DA, cmpBytes, isVirtualAddress: true));

            return this;
        }

        // ── Apply ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a timestamped backup then applies all queued patches to the executable.
        /// Returns a summary of all operations performed.
        /// </summary>
        public PatchResult Apply()
        {
            if (_pendingPatches.Count == 0)
                return new PatchResult(false, "No patches queued.", Array.Empty<string>());

            // ── Create backup ─────────────────────────────────────────────
            try
            {
                Directory.CreateDirectory(_backupDir);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFileName = $"SR_GameServer_{timestamp}.exe";
                string backupPath = Path.Combine(_backupDir, backupFileName);
                File.Copy(_exePath, backupPath, overwrite: false);
            }
            catch (Exception ex)
            {
                return new PatchResult(false, $"Backup failed: {ex.Message}. Aborting patch.", Array.Empty<string>());
            }

            // ── Resolve virtual addresses if needed ───────────────────────
            long vaToFileAdjustment = 0;
            bool needsVAConversion = _pendingPatches.Any(p => p.IsVirtualAddress);

            if (needsVAConversion)
            {
                try
                {
                    vaToFileAdjustment = CalculateVAToFileOffset(_exePath);
                }
                catch (Exception ex)
                {
                    _pendingPatches.Clear();
                    return new PatchResult(false, $"Failed to parse PE headers for VA conversion: {ex.Message}", Array.Empty<string>());
                }
            }

            // ── Apply patches ─────────────────────────────────────────────
            var applied = new List<string>();

            try
            {
                using var fs = new FileStream(_exePath, FileMode.Open, FileAccess.ReadWrite);
                using var writer = new BinaryWriter(fs);
                using var reader = new BinaryReader(fs);

                foreach (var patch in _pendingPatches)
                {
                    long fileOffset = patch.IsVirtualAddress
                        ? patch.Offset - vaToFileAdjustment
                        : patch.Offset;

                    // Bounds check
                    if (fileOffset < 0 || fileOffset + patch.Data.Length > fs.Length)
                    {
                        _pendingPatches.Clear();
                        return new PatchResult(false,
                            $"Patch '{patch.Name}' at offset 0x{fileOffset:X} (VA: 0x{patch.Offset:X}) " +
                            $"exceeds file bounds (file size: {fs.Length} bytes). " +
                            "Is this the correct SR_GameServer.exe?",
                            applied);
                    }

                    // Verification: if expected bytes are provided, check them first
                    if (patch.ExpectedBytes != null)
                    {
                        fs.Seek(fileOffset, SeekOrigin.Begin);
                        byte[] existing = reader.ReadBytes(patch.ExpectedBytes.Length);

                        // Allow if bytes match the default (unpatched) OR already match target (re-patch)
                        bool matchesDefault = existing.SequenceEqual(patch.ExpectedBytes);
                        bool matchesTarget = existing.SequenceEqual(patch.Data);

                        if (!matchesDefault && !matchesTarget)
                        {
                            string existingHex = BitConverter.ToString(existing).Replace("-", " ");
                            string expectedHex = BitConverter.ToString(patch.ExpectedBytes).Replace("-", " ");
                            _pendingPatches.Clear();
                            return new PatchResult(false,
                                $"Verification failed for '{patch.Name}' at file offset 0x{fileOffset:X}. " +
                                $"Expected [{expectedHex}] but found [{existingHex}]. " +
                                "The executable may already be patched with a different value, or this is not a standard vSRO 1.188 binary.",
                                applied);
                        }
                    }

                    // Write the patch
                    fs.Seek(fileOffset, SeekOrigin.Begin);
                    writer.Write(patch.Data, 0, patch.Data.Length);
                    applied.Add(patch.Name);
                }

                writer.Flush();
            }
            catch (IOException ex)
            {
                return new PatchResult(false, $"File access error: {ex.Message}. Is the GameServer running?", applied);
            }
            catch (Exception ex)
            {
                return new PatchResult(false, $"Unexpected error: {ex.Message}", applied);
            }
            finally
            {
                _pendingPatches.Clear();
            }

            return new PatchResult(true, $"Applied {applied.Count} patch(es) successfully.", applied);
        }

        // ── PE Header Parsing ─────────────────────────────────────────────────

        /// <summary>
        /// Parses the PE headers to calculate the adjustment needed to convert
        /// a virtual address to a raw file offset.
        /// 
        /// For a given VA in the .text section:
        ///   FileOffset = VA - SectionVirtualAddress + SectionRawDataPointer
        /// 
        /// Returns the value (SectionVirtualAddress - SectionRawDataPointer + ImageBase)
        /// so that: FileOffset = VA - returnValue
        /// </summary>
        private static long CalculateVAToFileOffset(string exePath)
        {
            using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            // DOS header: e_lfanew at offset 0x3C gives PE header location
            fs.Seek(0x3C, SeekOrigin.Begin);
            int peHeaderOffset = reader.ReadInt32();

            // PE signature (4 bytes) + COFF header (20 bytes)
            fs.Seek(peHeaderOffset + 4, SeekOrigin.Begin);

            // COFF Header
            ushort machine = reader.ReadUInt16();          // +0
            ushort numberOfSections = reader.ReadUInt16();  // +2
            reader.ReadBytes(12);                           // skip TimeDateStamp, PointerToSymbolTable, NumberOfSymbols
            ushort sizeOfOptionalHeader = reader.ReadUInt16(); // +16
            ushort characteristics = reader.ReadUInt16();   // +18

            // Optional Header starts here
            long optionalHeaderStart = fs.Position;
            ushort magic = reader.ReadUInt16(); // PE32 = 0x10B, PE32+ = 0x20B

            // ImageBase is at different offsets for PE32 vs PE32+
            uint imageBase;
            if (magic == 0x10B) // PE32
            {
                fs.Seek(optionalHeaderStart + 28, SeekOrigin.Begin);
                imageBase = reader.ReadUInt32();
            }
            else // PE32+ (unlikely for vSRO but handle it)
            {
                fs.Seek(optionalHeaderStart + 24, SeekOrigin.Begin);
                imageBase = (uint)reader.ReadUInt64();
            }

            // Section headers start after optional header
            fs.Seek(optionalHeaderStart + sizeOfOptionalHeader, SeekOrigin.Begin);

            // Find the .text section (first code section, usually the first one)
            for (int i = 0; i < numberOfSections; i++)
            {
                byte[] nameBytes = reader.ReadBytes(8);
                string sectionName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

                uint virtualSize = reader.ReadUInt32();        // +8
                uint virtualAddress = reader.ReadUInt32();      // +12
                uint rawDataSize = reader.ReadUInt32();         // +16
                uint rawDataPointer = reader.ReadUInt32();      // +20
                reader.ReadBytes(16);                           // skip remaining fields

                if (sectionName == ".text" || i == 0)
                {
                    // VA = ImageBase + SectionVA + offset_within_section
                    // FileOffset = SectionRawPointer + offset_within_section
                    // So: FileOffset = VA - ImageBase - SectionVA + SectionRawPointer
                    // Adjustment = ImageBase + SectionVA - SectionRawPointer
                    return (long)imageBase + (long)virtualAddress - (long)rawDataPointer;
                }
            }

            throw new Exception("Could not find .text section in PE headers.");
        }

        // ── Supporting Types ──────────────────────────────────────────────────

        private class PatchOperation
        {
            public string Name { get; }
            public long Offset { get; }
            public byte[] Data { get; }
            public bool IsVirtualAddress { get; }
            public byte[]? ExpectedBytes { get; }

            public PatchOperation(string name, long offset, byte[] data, bool isVirtualAddress = false, byte[]? expectedBytes = null)
            {
                Name = name;
                Offset = offset;
                Data = data;
                IsVirtualAddress = isVirtualAddress;
                ExpectedBytes = expectedBytes;
            }
        }

        public record PatchResult(bool Success, string Message, IReadOnlyList<string> AppliedPatches);
    }
}