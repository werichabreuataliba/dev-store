using Confluent.Kafka;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevStore.MessageBus.Serializador
{
    internal class SerializerDevStore<T> : ISerializer<T>
    {
        public byte[] Serialize(T data, SerializationContext context)
        {
            var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data);
            using var compressedStream = new MemoryStream();
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress, true);
            gzipStream.Write(bytes, 0, bytes.Length);
            gzipStream.Close();
            var buffer = compressedStream.ToArray();                
            return buffer;
            
        }
    }
}
