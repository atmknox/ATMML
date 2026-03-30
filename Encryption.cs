using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Specialized;

namespace ATMML
{
    public class Encryption
    {
		public static string Message(string url, string[] input)
		{
			NameValueCollection data = new NameValueCollection();
			//for (int ii = 0; ii < input.Length; ii++) data["x" + (ii + 1).ToString()] = encrypt(input[ii]);
			for (int ii = 0; ii < input.Length; ii++) data["x" + (ii + 1).ToString()] = input[ii];
			EncryptionWebClient wc = new EncryptionWebClient();
			byte[] response = wc.UploadValues(url, data);
			var msg = System.Text.Encoding.UTF8.GetString(response);
			string output = msg; // decrypt(msg);
			return output;
		}

		public static string Encrypt(string input)
		{
			return input; // encrypt(input);
		}

		public static string Decrypt(string input)
		{
			return input; // decrypt(input);
		}

		private static string encrypt(string text)
        {
			string output = "";
			try
			{
				MemoryStream ms = new MemoryStream();
				RijndaelManaged aes = new RijndaelManaged();
				aes.KeySize = 256;
				aes.BlockSize = 256;
				aes.Padding = PaddingMode.Zeros;
				aes.Mode = CipherMode.CBC;
				aes.IV = Convert.FromBase64String(iv);
				var ky = Convert.FromBase64String(key);
				ICryptoTransform encryptor = aes.CreateEncryptor(ky, aes.IV);
				byte[] _text = Encoding.Default.GetBytes(text);
				CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
				cs.Write(_text, 0, _text.Length);
				cs.FlushFinalBlock();
				byte[] encdata = ms.ToArray();
				ms.Close();
				cs.Close();
			    output = Convert.ToBase64String(encdata);
			}
			catch (Exception x)
			{
				System.Diagnostics.Debug.WriteLine(x.Message);
			}
			return output;
        }

        private static string decrypt(string text)
        {
			string output = "";
			try
			{
				MemoryStream ms = new MemoryStream(Convert.FromBase64String(text));
				RijndaelManaged aes = new RijndaelManaged();
				aes.KeySize = 256;
				aes.BlockSize = 256;
				aes.Padding = PaddingMode.Zeros;
				aes.Mode = CipherMode.CBC;
				aes.IV = Convert.FromBase64String(iv);
				var ky = Convert.FromBase64String(key);
				ICryptoTransform decryptor = aes.CreateDecryptor(ky, aes.IV);
				CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
				byte[] _data = new byte[ms.Length];
				int i = cs.Read(_data, 0, _data.Length);
				output = Encoding.Default.GetString(_data, 0, i).Replace("\0", "");
			}
			catch(Exception x)
			{
				System.Diagnostics.Debug.WriteLine(x.Message);
			}
			return output;
        }

        private static string key
        {
            get { return "hujsha8yushhjhey7t35549jnnbhjQQQkmikM1xMxMN=";}
        }

        private static string iv
        {
            get { return "bhhuhu72sttDQertT5UxjihubugDFRdAxpllxxdfxgh=";}
        }

        private class EncryptionWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest w = base.GetWebRequest(uri);
                w.Timeout = 5 * 60 * 1000;
                return w;
            }
        }
    }
}
