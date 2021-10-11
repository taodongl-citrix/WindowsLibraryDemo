using System.IO;
using System.Text;

namespace WindowsLibraryDemo.Logger.Tracing
{
    public interface ITraceWriter
    {
        /// <summary>
        /// Flushes the output buffer and then closes the output.
        /// </summary>
        void Close();

        /// <summary>
        /// Flushes the output buffer.
        /// </summary>
        void Flush();

        /// <summary>
        /// Writes a message followed by a line terminator.
        /// </summary>
        /// <param name="message">A message to write.</param>
        void WriteLine(string message);
    }
    public class FileTraceWriter : ITraceWriter
    {
        private readonly TextWriter textWriter;

        public FileTraceWriter(string path, bool append, Encoding endoding, int buffer)
        {
            textWriter = new StreamWriter(path, append, endoding, buffer);
        }

        public void Close()
        {
            textWriter.Close();
        }

        public void Flush()
        {
            textWriter.Flush();
        }

        public void WriteLine(string message)
        {
            textWriter.WriteLine(message);
        }
    }
}
