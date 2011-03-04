using System;
using System.Linq;
using System.IO;

namespace TestProject {

    class SFOReader {
        SFOTableEntry[] table;

        public SFOReader(string filename) {
            using(BinaryReader r = new BinaryReader(File.OpenRead(filename))) {
                string filetype = new string(r.ReadChars(4));  // 00  P  S  F
                byte[] version = r.ReadBytes(4);               // 01 01 00 00
                int keyTableStart = r.ReadInt32();
                int valueTableStart = r.ReadInt32();
                int itemCount = r.ReadInt32();

                string[] keys = new string[itemCount];
                table = new SFOTableEntry[itemCount];

                for(int i = 0; i < itemCount; i++) {
                    short keyOffset = r.ReadInt16();
                    byte alignment = r.ReadByte();
                    byte datatype = r.ReadByte();
                    int valueSize = r.ReadInt32();
                    int valueSizeWithPadding = r.ReadInt32();
                    int valueOffset = r.ReadInt32();

                    long pos = r.BaseStream.Position;
                    r.BaseStream.Position = keyTableStart + keyOffset;
                    string key = string.Empty;
                    char c;
                    while((c = r.ReadChar()) != '\0')
                        key += c;
                    keys[i] = key;

                    r.BaseStream.Position = valueTableStart + valueOffset;
                    object value = default(object);

                    if(datatype == 0)
                        value = r.ReadBytes(valueSize);
                    else if(datatype == 2)
                        value = new string(r.ReadChars(valueSize)).TrimEnd('\0');
                    else if(datatype == 4)
                        value = r.ReadInt32();

                    r.ReadBytes(valueSizeWithPadding - valueSize);

                    table[i] = new SFOTableEntry(i, datatype, keys[i], value);
                    r.BaseStream.Position = pos;
                }

                r.Close();
            }
        }

        public string[] Keys {
            get {
                return table.Select(e => e.Key).ToArray();
            }
        }

        public object this[int index] {
            get {
                return table.Where(e => e.Position == index).Select(e => e.Value).SingleOrDefault();
            }
        }

        public object this[string key] {
            get {
                return table.Where(e => e.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase)).Select(e => e.Value).SingleOrDefault();
            }
        }
    }
    enum SFODataType {
        Binary = 0,
        String = 2,
        Int32 = 4
    };
    class SFOTableEntry {
        public SFOTableEntry(int pos, byte type, string key, object value) {
            Position = pos;
            Datatype = (SFODataType)type;
            Key = key;
            Value = value;
        }
        public int Position {
            get;
            private set;
        }
        public SFODataType Datatype {
            get;
            private set;
        }
        public string Key {
            get;
            private set;
        }
        public object Value {
            get;
            private set;
        }
    }
}
