using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Storage;

namespace Explorer.Logic.FileSystemService
{
    public static partial class FileSystem
    {
        public static async void SerializeObject(object data, string name)
        {
            var sData = JsonConvert.SerializeObject(data);
            var buffer = CryptographicBuffer.ConvertStringToBinary(sData, BinaryStringEncoding.Utf8);

            var file = await CreateOrOpenFileAsync(AppDataFolder, name, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteBufferAsync(file, buffer);
        }

        public static async Task<T> DeserializeObject<T>(string name)
        {
            var file = await CreateOrOpenFileAsync(AppDataFolder, name);
            var buffer = await FileIO.ReadBufferAsync(file);

            var serializedObject = CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, buffer);
            return JsonConvert.DeserializeObject<T>(serializedObject);
        }
    }
}
