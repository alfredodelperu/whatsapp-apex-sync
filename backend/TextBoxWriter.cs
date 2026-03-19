using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WhatsAppTranscriptor
{
    public class TextBoxWriter : TextWriter
    {
        private TextBox _output;

        public TextBoxWriter(TextBox output)
        {
            _output = output;
        }

        public override void Write(char value)
        {
            base.Write(value);
            WriteToTextBox(value.ToString());
        }

        public override void Write(string? value)
        {
            base.Write(value);
            if (value != null) WriteToTextBox(value);
        }

        public override Encoding Encoding => Encoding.UTF8;

        private void WriteToTextBox(string text)
        {
            if (_output.IsHandleCreated && !_output.IsDisposed)
            {
                if (_output.InvokeRequired)
                {
                    _output.Invoke(new Action<string>(WriteToTextBox), new object[] { text });
                }
                else
                {
                    // Scroll to bottom
                    _output.AppendText(text);
                }
            }
        }
    }
}
