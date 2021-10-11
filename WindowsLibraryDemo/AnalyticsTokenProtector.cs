using System;
using System.Text;
using System.IO;

namespace WindowsLibraryDemo
{
    class CitrixAnalyticsTokenProtector
    {
        private static byte[] m_FixedEntropy = null;
        private static Guid m_GUID = new Guid("{3CC3B741-44e7-1170-A7FE-330325494079}");

        private static string GetEncryptionFilePath(string strStoreServiceRecordID)
        {
            string strPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            strPath += "\\Demo\\SelfService\\" + strStoreServiceRecordID + ".dat";

            return strPath;
        }

        private static byte[] Xor(byte[] a, byte[] b)
        {
            int l = Math.Max(a.Length, b.Length);

            byte[] rc = new byte[l];

            for (int i = 0; i < l; i++)
            {
                byte ab = a[i % a.Length];
                byte bb = b[i % b.Length];
                rc[i] = (byte)(ab ^ bb);
            }
            return rc;
        }

        public static void CtxEncryptData(string strStoreConfigURL, string strStoreServiceRecordID, string strTokenData)
        {
            FileStream fStream = null;
            try
            {
                string strEncryptionFilePath = GetEncryptionFilePath(strStoreServiceRecordID);
                byte[] entropy = null;

                Tracer.DServices.Trace("CAS - CtxEncryptData : Enter");

                if (m_FixedEntropy == null)
                    m_FixedEntropy = UnicodeEncoding.ASCII.GetBytes(m_GUID.ToString());

                entropy = Xor(UnicodeEncoding.ASCII.GetBytes(strStoreConfigURL), m_FixedEntropy);

                // Create the original data to be encrypted
                byte[] toEncrypt = UnicodeEncoding.ASCII.GetBytes(strTokenData);

                // Encrypt the data in memory. The result is stored in the same same array as the original data.
                byte[] encryptedData = new byte[1024];

                Tracer.DServices.Trace("CAS - CtxEncryptData : Encrypting..");

                bool fileExists = File.Exists(strEncryptionFilePath);
                if (!fileExists)
                {
                    fStream = new FileStream(strEncryptionFilePath, FileMode.Create);
                }

                // Write the encrypted data to a stream.
                if (encryptedData != null)
                {
                    if (fileExists)
                        fStream = new FileStream(strEncryptionFilePath, FileMode.Truncate);
                    if (fStream.CanWrite)
                        fStream.Write(encryptedData, 0, encryptedData.Length);
                    Tracer.DServices.Trace("CAS - CtxEncryptData : Data written to file.");
                }

                fStream.Close();

            }
            catch (Exception e)
            {
                if (fStream != null)
                    fStream.Close();
                Tracer.DServices.Error("CAS - CtxEncryptData : Exception with message {0}", e.Message);
            }

        }

        private static int ReadBytes(FileStream stream, byte[] buffer)
        {
            int start = 0;
            for (; ; )
            {
                int next = buffer.Length - start;
                int part = stream.Read(buffer, start, next);
                if (part == 0)
                    break;
                start += part;
                if (start == buffer.Length)
                    break;
            }
            return start;
        }

        public static string CtxDecryptDatastring(string strStoreConfigURL, string strStoreServiceRecordID)
        {
            string strEncryptionFilePath = GetEncryptionFilePath(strStoreServiceRecordID);
            Tracer.DServices.Trace("CAS - CtxDecryptDatastring : Enter.");
            // write to strEncryptionFilePath
            byte[] buff = new byte[1024];
            return UnicodeEncoding.ASCII.GetString(buff);
        }
    }
}