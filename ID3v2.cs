using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Test.ID3 {

    #region - Abstract Frame Reader -
    /// <summary>
    /// Base class for reading ID3v2 frames.  Subclass this for new minor versions of ID3v2.
    /// </summary>
    public abstract class ID3v2Frames {
        protected FileStream mp3;
        protected List<Tag> tags;
        protected List<Image> images;
        protected bool read;

        protected ID3v2Frames(FileStream MP3Stream) {
            mp3 = MP3Stream;
            tags = new List<Tag>();
            images = new List<Image>();
        }

        internal abstract void Read(int TagLength);
        protected abstract Image getImage(byte[] frame);
        protected bool badFrameID(string frameID) {
            return !frameID.All(c => (char.IsLetter(c) && char.IsUpper(c)) || char.IsDigit(c));
        }
        protected int getFrameSize(byte[] rawSize) {
            int retVal = -1;
            for(int i = rawSize.Length - 1, j = 0; i >= 0; i--, j += 7) {
                retVal += rawSize[i] << j;
            }
            return retVal;
        }

        /// <summary>
        /// Gets a collection of tags found in the MP3
        /// </summary>
        public Tag[] Tags {
            get;
            protected set;
        }
        /// <summary>
        /// Gets a collection of artwork found in the MP3
        /// </summary>
        public Image[] Images {
            get;
            protected set;
        }
    }
    #endregion

    #region - 2.2 Frame Reader -
    /// <summary>
    /// Reads the frames contained in a 2.2 ID3v2 tag
    /// </summary>
    public class V22Frames : ID3v2Frames {
        public V22Frames(FileStream MP3Stream)
            : base(MP3Stream) {
        }

        internal override void Read(int TagLength) {
            if(!read) {
                while(true) {
                    byte[] rawFrameID = new byte[3];
                    byte[] rawSize = new byte[3];
                    byte[] frameData;

                    mp3.Read(rawFrameID, 0, rawFrameID.Length);
                    string frameID = Encoding.ASCII.GetString(rawFrameID);

                    if(badFrameID(frameID))
                        break;

                    mp3.Read(rawSize, 0, rawSize.Length);
                    int frameSize = getFrameSize(rawSize);

                    if(frameSize == -1)
                        break;

                    byte encByte = (byte)mp3.ReadByte();

                    // discarding the language portion of the COM tag
                    if(frameID == "COM") {
                        mp3.Position += 4;
                        frameSize -= 4;
                    }

                    frameData = new byte[frameSize];

                    mp3.Read(frameData, 0, frameData.Length);

                    if(frameID != "PIC") {
                        tags.Add(new Tag(frameID, frameData, encByte));
                    } else {
                        images.Add(getImage(frameData));
                    }

                    if(mp3.Position >= TagLength)
                        break;
                }
                Tags = tags.ToArray();
                Images = images.ToArray();
                read = true;
            }
        }
        protected override Image getImage(byte[] frame) {
            byte[] img;

            using(MemoryStream ms = new MemoryStream(frame)) {
                StringBuilder desc = new StringBuilder();
                byte[] imgFormat = new byte[3];
                byte picType, b;

                ms.Read(imgFormat, 0, imgFormat.Length);
                picType = (byte)ms.ReadByte();
                while((b = ((byte)ms.ReadByte())) != 0x00)
                    desc.Append((char)b);

                img = new byte[frame.Length - ms.Position];
                ms.Read(img, 0, img.Length);
                ms.Close();
            }

            return Image.FromStream(new MemoryStream(img));
        }
    }
    #endregion

    #region - 2.3 Frame Reader -
    /// <summary>
    /// Reads the frames contained in a 2.3 ID3v2 tag
    /// </summary>
    public class V23Frames : ID3v2Frames {
        protected Dictionary<string, V23Flags> tagFlags;
        protected bool hasExtHeader;

        internal V23Frames(FileStream MP3Stream)
            : this(MP3Stream, false) {
        }
        internal V23Frames(FileStream MP3Stream, bool HasExtendedHeader)
            : base(MP3Stream) {
            hasExtHeader = HasExtendedHeader;
        }

        internal override void Read(int TagLength) {
            if(!read) {
                tagFlags = new Dictionary<string, V23Flags>();

                if(hasExtHeader) {
                    // consuming the extended header, but not caring about it
                    byte[] exHeader = new byte[10];
                    mp3.Read(exHeader, 0, exHeader.Length);
                }

                while(true) {
                    byte[] rawFrameID = new byte[4];
                    byte[] rawSize = new byte[4];
                    byte[] rawFlags = new byte[2];
                    byte[] frameData;

                    mp3.Read(rawFrameID, 0, rawFrameID.Length);
                    string frameID = Encoding.ASCII.GetString(rawFrameID);

                    if(badFrameID(frameID))
                        break;

                    mp3.Read(rawSize, 0, rawSize.Length);
                    int frameSize = getFrameSize(rawSize);

                    if(frameSize == -1)
                        break;

                    // discarding the language portion of the COMM tag
                    if(frameID == "COMM") {
                        mp3.Position += 4;
                        frameSize -= 4;
                    }

                    // skipping the PRIV tag, too much of a pain.
                    if(frameID == "PRIV") {
                        mp3.Position += frameSize + 3;
                        continue;
                    }

                    frameData = new byte[frameSize];

                    mp3.Read(rawFlags, 0, rawFlags.Length);
                    tagFlags[frameID] = new V23Flags(rawFlags);

                    byte encByte = (byte)mp3.ReadByte();

                    mp3.Read(frameData, 0, frameData.Length);

                    if(frameID != "APIC") {
                        tags.Add(new Tag(frameID, frameData, encByte));
                    } else {
                        images.Add(getImage(frameData));
                    }

                    if(mp3.Position >= TagLength)
                        break;
                }
                Tags = tags.ToArray();
                Images = images.ToArray();
                read = true;
            }
        }
        protected override Image getImage(byte[] frame) {
            byte[] img;

            using(MemoryStream ms = new MemoryStream(frame)) {
                StringBuilder mimeType = new StringBuilder(), desc = new StringBuilder();
                byte picType, b;

                while((b = ((byte)ms.ReadByte())) != 0x00)
                    mimeType.Append((char)b);
                picType = (byte)ms.ReadByte();
                while((b = ((byte)ms.ReadByte())) != 0x00)
                    desc.Append((char)b);

                img = new byte[frame.Length - ms.Position];
                ms.Read(img, 0, img.Length);
                ms.Close();
            }

            return Image.FromStream(new MemoryStream(img));
        }
        /// <summary>
        /// Gets the flag values for a tag
        /// </summary>
        /// <param name="TagName">Tag to obtain flags for</param>
        /// <returns></returns>
        public V23Flags GetFlags(string TagName) {
            return tagFlags[TagName];
        }
    }
    #endregion

    #region - 2.4 Frame Reader -
    /// <summary>
    /// Reads the frames contained in a 2.4 ID3v2 tag
    /// </summary>
    public class V24Frames : ID3v2Frames {
        protected Dictionary<string, V24Flags> tagFlags;
        protected bool hasExtHeader;

        internal V24Frames(FileStream MP3Stream)
            : this(MP3Stream, false) {
        }
        internal V24Frames(FileStream MP3Stream, bool HasExtendedHeader)
            : base(MP3Stream) {
            hasExtHeader = HasExtendedHeader;
        }

        internal override void Read(int TagLength) {
            if(!read) {
                tagFlags = new Dictionary<string, V24Flags>();

                if(hasExtHeader) {
                    // Once again, consuming the extended header but not caring about it.
                    byte[] rawHeaderLen = new byte[4];
                    byte[] extHeader;
                    int headerLen;
                    mp3.Read(rawHeaderLen, 0, rawHeaderLen.Length);
                    headerLen = (rawHeaderLen[0] << 21) + (rawHeaderLen[1] << 14) + (rawHeaderLen[2] << 7) + rawHeaderLen[3];
                    extHeader = new byte[headerLen];
                    mp3.Read(extHeader, 0, extHeader.Length);
                }

                while(true) {
                    byte[] rawFrameID = new byte[4];
                    byte[] rawSize = new byte[4];
                    byte[] rawFlags = new byte[2];
                    byte[] frameData;

                    mp3.Read(rawFrameID, 0, rawFrameID.Length);
                    string frameID = Encoding.ASCII.GetString(rawFrameID);

                    if(badFrameID(frameID))
                        break;

                    mp3.Read(rawSize, 0, rawSize.Length);
                    int frameSize = getFrameSize(rawSize);

                    if(frameSize == -1)
                        break;

                    // discarding the language portion of the COMM tag
                    if(frameID == "COMM") {
                        mp3.Position += 4;
                        frameSize -= 4;
                    }

                    // skipping the PRIV tag, too much of a pain.
                    if(frameID == "PRIV") {
                        mp3.Position += frameSize + 3;
                        continue;
                    }

                    frameData = new byte[frameSize];

                    mp3.Read(rawFlags, 0, rawFlags.Length);
                    tagFlags[frameID] = new V24Flags(rawFlags);

                    byte encByte = (byte)mp3.ReadByte();

                    mp3.Read(frameData, 0, frameData.Length);

                    if(frameID != "APIC") {
                        tags.Add(new Tag(frameID, frameData, encByte));
                    } else {
                        images.Add(getImage(frameData));
                    }

                    if(mp3.Position >= TagLength)
                        break;
                }
                Tags = tags.ToArray();
                Images = images.ToArray();
                read = true;
            }
        }
        protected override Image getImage(byte[] frame) {
            byte[] img;

            using(MemoryStream ms = new MemoryStream(frame)) {
                StringBuilder mimeType = new StringBuilder(), desc = new StringBuilder();
                byte picType, b;

                while((b = ((byte)ms.ReadByte())) != 0x00)
                    mimeType.Append((char)b);
                picType = (byte)ms.ReadByte();
                while((b = ((byte)ms.ReadByte())) != 0x00)
                    desc.Append((char)b);

                img = new byte[frame.Length - ms.Position];
                ms.Read(img, 0, img.Length);
                ms.Close();
            }

            return Image.FromStream(new MemoryStream(img));
        }

        /// <summary>
        /// Gets the flag values for a tag
        /// </summary>
        /// <param name="TagName">Tag to obtain flags for</param>
        /// <returns></returns>
        public V24Flags GetFlags(string TagName) {
            return tagFlags[TagName];
        }
    }
    #endregion

    #region - Flag Structs -
    /// <summary>
    /// Flag information for 2.3 ID3v2 frames
    /// </summary>
    public struct V23Flags {
        byte[] f;

        /// <summary>
        /// Parsing 2.3 frame flags into booleans
        /// </summary>
        /// <param name="Flags">2-byte flag array</param>
        public V23Flags(byte[] Flags) {
            f = Flags;
        }

        /// <summary>
        /// Gets whether or not the frame should be discarded if the tag is altered
        /// </summary>
        public bool DiscardFrameIfTagChanges {
            get {
                return (f[0] & 0x80) == 0x80;
            }
        }
        /// <summary>
        /// Gets whether or not the frame should be discarded if the file is altered
        /// </summary>
        public bool DiscardFrameIfFileChanges {
            get {
                return (f[0] & 0x40) == 0x40;
            }
        }
        /// <summary>
        /// Gets whether or not the contents of the frame are read only
        /// </summary>
        public bool ReadOnly {
            get {
                return (f[0] & 0x20) == 0x20;
            }
        }
        /// <summary>
        /// Gets whether or not the frame is compressed
        /// </summary>
        public bool Compressed {
            get {
                return (f[1] & 0x80) == 0x80;
            }
        }
        /// <summary>
        /// Gets whether or not the frame is encrypted
        /// </summary>
        public bool Encrypted {
            get {
                return (f[1] & 0x40) == 0x40;
            }
        }
        /// <summary>
        /// Gets whether or not the frame belongs in a group with other frames
        /// </summary>
        public bool IsGrouped {
            get {
                return (f[1] & 0x20) == 0x20;
            }
        }
    }
    /// <summary>
    /// Flag information for 2.4 ID3v2 frames
    /// </summary>
    public struct V24Flags {
        byte[] f;

        /// <summary>
        /// Parsing 2.4 frame flags into booleans
        /// </summary>
        /// <param name="Flags">2-byte flag array</param>
        public V24Flags(byte[] Flags) {
            f = Flags;
        }

        /// <summary>
        /// Gets whether or not the frame should be discarded if the tag is altered
        /// </summary>
        public bool DiscardFrameIfTagChanges {
            get {
                return (f[0] & 0x40) == 0x40;
            }
        }
        /// <summary>
        /// Gets whether or not the frame should be discarded if the file is altered
        /// </summary>
        public bool DiscardFrameIfFileChanges {
            get {
                return (f[0] & 0x20) == 0x20;
            }
        }
        /// <summary>
        /// Gets whether or not the contents of the frame are read only
        /// </summary>
        public bool ReadOnly {
            get {
                return (f[0] & 0x10) == 0x10;
            }
        }
        /// <summary>
        /// Gets whether or not the frame belongs in a group with other frames
        /// </summary>
        public bool IsGrouped {
            get {
                return (f[1] & 0x40) == 0x40;
            }
        }
        /// <summary>
        /// Gets whether or not the frame is compressed
        /// </summary>
        public bool Compressed {
            get {
                return (f[1] & 0x08) == 0x08;
            }
        }
        /// <summary>
        /// Gets whether or not the frame is encrypted
        /// </summary>
        public bool Encrypted {
            get {
                return (f[1] & 0x04) == 0x04;
            }
        }
        /// <summary>
        /// Gets whether or not the frame has been unsynchronised
        /// </summary>
        public bool Unsynced {
            get {
                return (f[1] & 0x02) == 0x02;
            }
        }
        /// <summary>
        /// Gets whether or not a data length indicator is present in the frame
        /// </summary>
        public bool HasDataLengthIndicator {
            get {
                return (f[1] & 0x01) == 0x01;
            }
        }
    }
    #endregion

    #region - Tag -
    /// <summary>
    /// Container for tag names and values
    /// </summary>
    public class Tag {
        internal Tag(string name, byte[] data, byte encoding) {
            Encoding enc;
            switch(encoding) {
                // UTF-16 encoded unicode w/ byte order marking
                case 1:
                // UTF-16 encoded unicode w/o byte order marking (2.4)
                case 2:
                    enc = Encoding.Unicode;
                    break;
                // UTF-8 encoded unicode (2.4)
                case 3:
                    enc = Encoding.UTF8;
                    break;
                // ASCII (ISO-8859-1)
                case 0:
                default:
                    enc = Encoding.ASCII;
                    break;
            }

            Name = name;
            Value = enc.GetString(data).Replace('\0', ' ').Trim();
        }

        /// <summary>
        /// Returns the string represendation of this object
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return string.Format("{0} - {1}", Name, Value);
        }

        /// <summary>
        /// The identifier of the tag
        /// </summary>
        public string Name {
            get;
            private set;
        }
        /// <summary>
        /// The value of the tag
        /// </summary>
        public string Value {
            get;
            private set;
        }
    }
    #endregion

    #region - ID3v2 -
    /// <summary>
    /// Reads tag information and artwork from MP3s.  Currently supports ID3v2 versions 2.2, 2.3 &amp; 2.4
    /// </summary>
    public class ID3v2 {
        ID3v2Frames frames;

        /// <summary>
        /// Reads tags for the given MP3
        /// </summary>
        /// <param name="Filename">Path of the MP3</param>
        public ID3v2(string Filename)
            : this(new FileInfo(Filename)) {
        }
        /// <summary>
        /// Reads tags for the given MP3
        /// </summary>
        /// <param name="Filename">FileInfo representation of the MP3</param>
        public ID3v2(FileInfo MP3) {
            byte[] header = new byte[6];
            byte[] tagSize = new byte[4];
            File = MP3;

            using(FileStream fs = MP3.OpenRead()) {
                fs.Read(header, 0, header.Length);
                fs.Read(tagSize, 0, tagSize.Length);

                HasID3v2Data = Encoding.ASCII.GetString(header, 0, 3) == "ID3";

                if(!HasID3v2Data) {
                    Version = "N/A";
                    fs.Close();
                    return;
                }

                Version = string.Format("ID3v2.{0}.{1}", header[3], header[4]);
                Flags = header[5];

                // converting syncsafe integer to a normal int                
                int tagLength = (tagSize[0] << 21) + (tagSize[1] << 14) + (tagSize[2] << 7) + tagSize[3];                
                tagLength -= HasExtendedHeader ? 20 : 10;

                VersionSupported = true;
                switch(header[3]) {
                    case 2:
                        frames = new V22Frames(fs);
                        break;
                    case 3:
                        frames = new V23Frames(fs, HasExtendedHeader);
                        break;
                    case 4:
                        frames = new V24Frames(fs, HasExtendedHeader);
                        break;
                    default:
                        VersionSupported = false;
                        frames = null;
                        break;
                }

                frames.Read(tagLength);
                fs.Close();
            }
        }

        /// <summary>
        /// Gets whether or not ID3v2 data is present in the MP3
        /// </summary>
        public bool HasID3v2Data {
            get;
            private set;
        }
        /// <summary>
        /// Gets whether or not the ID3v2 version is currently supported
        /// </summary>
        public bool VersionSupported {
            get;
            private set;
        }
        byte Flags {
            get;
            set;
        }
        /// <summary>
        /// Gets the full tag version found the file
        /// </summary>
        public string Version {
            get;
            private set;
        }
        /// <summary>
        /// Gets whether or not unsynchronisation is used
        /// </summary>
        public bool Unsynced {
            get {
                return (Flags & 0x80) == 0x80;
            }
        }
        /// <summary>
        /// Gets whether or not the MP3 contains an extended header
        /// </summary>
        public bool HasExtendedHeader {
            get {
                return (Flags & 0x40) == 0x40;
            }
        }
        /// <summary>
        /// Gets whether or not the tag is in the experimental stage
        /// </summary>
        public bool Experimental {
            get {
                return (Flags & 0x20) == 0x20;
            }
        }
        /// <summary>
        /// Gets whether or not the tag contains a footer (2.4+)
        /// </summary>
        public bool ContainsFooter {
            get {
                return (Flags & 0x10) == 0x10;
            }
        }
        /// <summary>
        /// Gets a collection of tags found in the MP3
        /// </summary>
        public Tag[] Tags {
            get {
                if(HasID3v2Data)
                    return frames.Tags;
                return null;
            }
        }
        /// <summary>
        /// Gets a collection of artwork found in the MP3
        /// </summary>
        public Image[] Images {
            get {
                if(HasID3v2Data)
                    return frames.Images;
                return null;
            }
        }
        /// <summary>
        /// Gets the FileInfo representation of the MP3
        /// </summary>
        public FileInfo File {
            get;
            private set;
        }
    }
    #endregion
}
