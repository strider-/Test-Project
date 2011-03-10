﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace TestProject {
    class LCD : IDisposable {
        SerialPort com;

        public enum LCDCursorType {
            Underline = 5,
            Block,
            InvertingBlock
        }
        public enum LCDGraphType {
            Thick = 255,
            Invisible = 0,
            ThinCenter = 16,
            ThinLow = 1,
            ThinHigh = 128,
            Striped = 85,
            MediumCenter = 60,
            MediumLow = 15,
            MediumHigh = 240
        }

        public LCD(string PortName) {
            com = new SerialPort(PortName, 19200, Parity.None, 8, StopBits.One);
            CursorType = LCDCursorType.InvertingBlock;
            com.Open();
        }

        public void Dispose() {
            com.Close();
        }
        public void Close() {
            com.Close();
        }
        void write(byte data) {
            write(new byte[] { data });
        }
        void write(byte[] data) {
            com.Write(data, 0, data.Length);
        }
        public void MoveHome() {
            write(1);
        }
        public void HideDisplay() {
            write(2);
        }
        public void ShowDisplay() {
            write(3);
        }
        public void HideCursor() {
            write(4);
        }
        public void ShowCursor() {
            write((byte)CursorType);
        }
        public void Backspace() {
            write(8);
        }
        public void LineFeed() {
            write(10);
        }
        public void DeleteInPlace() {
            write(11);
        }
        public void FormFeed() {
            write(12);
        }
        public void CarriageReturn() {
            write(13);
        }
        public void CRLF() {
            write(new byte[] { 13, 0x10 });
        }
        public void SetBacklight(byte Value) {
            if(Value < 0)
                Value = 0;
            if(Value > 100)
                Value = 100;

            write(new byte[] { 14, Value });
        }
        public void SetContrast(byte Value) {
            if(Value < 0)
                Value = 0;
            if(Value > 100)
                Value = 100;

            write(new byte[] { 15, Value });
        }
        public void SetCursorPosition(byte Column, byte Row) {
            if(Column < 0)
                Column = 0;
            if(Column > 19)
                Column = 19;
            if(Row < 0)
                Row = 0;
            if(Row > 3)
                Row = 3;

            write(new byte[] { 17, Column, Row });
        }
        public void Scroll(bool On) {
            write((byte)(On ? 19 : 20));
        }
        public void Wrap(bool On) {
            write((byte)(On ? 23 : 24));
        }
        public void DisplayInfoScreen() {
            write(31);
        }
        public void WriteText(string Text, byte Column, byte Row) {
            SetCursorPosition(Column, Row);
            com.Write(Text);
        }
        public void WriteText(string Text) {
            com.Write(Text);
        }
        public void Reboot() {
            write(new byte[] { 26, 26 });
        }
        public void ShowGraph(LCDGraphType type, byte Row, byte StartColumn, byte EndColumn, float Progress) {
            if(StartColumn > EndColumn)
                throw new ArgumentException("StartColumn must be less than or equal to EndColumn");

            if(StartColumn < 0)
                StartColumn = 0;
            if(StartColumn > 19)
                StartColumn = 19;
            if(EndColumn < 0)
                EndColumn = 0;
            if(EndColumn > 19)
                EndColumn = 19;
            if(Row < 0)
                Row = 0;
            if(Row > 3)
                Row = 3;
            if(Progress < 0)
                Progress = 0;
            if(Progress > 1)
                Progress = 1;

            int maxLen = ((EndColumn - StartColumn) + 1) * 6;
            byte length = (byte)(maxLen * Progress);

            write(new byte[] { 18, 0, (byte)type, StartColumn, EndColumn, length, Row });
        }

        public void Marquee(string Text, byte Row, byte Speed) {
            if(Speed <= 0)
                Speed = 1;
            if(Speed > 6)
                Speed = 6;

            StopMarquee();

            if(Text.Length <= 20) {
                WriteText(Text, 0, 0);
            } else {
                string visible = Text.Substring(0, 20);
                string hidden;
                if(Text.Length > 40)
                    hidden = Text.Substring(20, 17) + "...";
                else
                    hidden = string.Format("{0,-20}",Text.Substring(20));

                WriteText(visible, 0, Row);
                for(int i = 0; i < hidden.Length; i++) {
                    write(new byte[] { 21, (byte)i, (byte)hidden[i] });
                }
            }

            write(new byte[] { 22, Row, Speed, 16 });
        }

        public void StopMarquee() {
            write(new byte[] { 22, 255, 1, 16 });
        }

        public LCDCursorType CursorType {
            get;
            set;
        }
    }
}
