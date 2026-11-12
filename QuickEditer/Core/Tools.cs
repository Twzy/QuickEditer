using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace QuickEditer.Core
{
    public class Tools
    {

        #region XML序列化
        public static void WriteByXML<T>(T p, string path)
        {
            Stream stream = null;
            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(T));

                stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                xs.Serialize(stream, p);
                stream.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }
        }


        public static T ReadByXML<T>(string path)
        {
            Stream stream = null;
            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(T));
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                XmlTextReader xmlread = new XmlTextReader(stream);
                xmlread.Normalization = false;
                T p = (T)xs.Deserialize(xmlread);
                stream.Close();
                return p;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }
        }
        #endregion

        #region MD5缓存

        public static string MD5(string src)
        {
            byte[] fromData = new byte[0];
            fromData = Encoding.UTF8.GetBytes(src);
            //md5
            return MD5(fromData);
        }
        public static string MD5(byte[] src)
        {
            byte[] fromData = src;
            //md5
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] md5tagData = md5.ComputeHash(fromData);
            string md5Str = BitConverter.ToString(md5tagData);
            return md5Str;
        }

        #endregion

    }
}
