using NAudio.Wave;
using RingByteBuffer;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;

namespace WhisperLoopbackStream
{
    internal class WhisperStream
    {
        public static async Task RunDetection(TranscriptStorage storage)
        {
            var modelName = "ggml-base.bin";
            var modelType = GgmlType.Base;
            if (!File.Exists(modelName))
            {
                Console.WriteLine($"Downloading Model {modelName}");
                using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(modelType);
                using var fileWriter = File.OpenWrite(modelName);
                await modelStream.CopyToAsync(fileWriter);
            }

            await StreamDetection(storage, modelName);
        }

        private static async Task FullDetection(TranscriptStorage storage, string fileName, string modelName)
        {
            using var factory = WhisperFactory.FromPath(modelName);

            var builder = factory.CreateBuilder()
                .WithLanguage("auto");

            //if (opt.Command == "translate")
            //{
            //    builder.WithTranslate();
            //}

            using var processor = builder.Build();

            using var fileStream = File.OpenRead(fileName);

            await foreach (var segment in processor.ProcessAsync(fileStream, CancellationToken.None))
            {
                var msg = $"New Segment: {segment.Start} ==> {segment.End} : {segment.Text}";
                Console.WriteLine(msg);
                storage.TranscriptList.Add(new Transcript() { SegmentStart = segment.Start, SegmentEnd = segment.End, Text = segment.Text });
            }
        }

        private static Task StreamDetection(TranscriptStorage storage, string modelName)
        {

            return Task.Run(() =>
            {
                void OnNewSegment(SegmentData segment)
                {
                    Debug.WriteLine($"CSSS {segment.Start} ==> {segment.End} : {segment.Text}");
                    storage.TranscriptList.Add(new Transcript() { SegmentStart = segment.Start, SegmentEnd = segment.End, Text = segment.Text });
                }

                using var factory = WhisperFactory.FromPath(modelName);
                var builder = factory.CreateBuilder()
                    .WithLanguage("auto")
                    .WithSegmentEventHandler(OnNewSegment);

                using var processor = builder.Build();

                using var capture = new WasapiLoopbackCapture();
                capture.WaveFormat = new WaveFormat(16000, 16, 1);
                //using var ringbufferstream = new RingBufferStream(1024 * 1024, false);
                //using var writer = new WaveFileWriter(ringbufferstream, capture.WaveFormat);
                MemoryStream memstream = new MemoryStream();
                {
                    capture.DataAvailable += (s, a) =>
                    {
                        //writer.Write(a.Buffer, 0, a.BytesRecorded);
                        memstream.Write(a.Buffer, 0, a.BytesRecorded);
                        if (memstream.Length > capture.WaveFormat.AverageBytesPerSecond * 5)
                        {
                            using var reader = new BinaryReader(memstream);
                            memstream.Seek(0, SeekOrigin.Begin);

                            var samplesCount = memstream.Length / 2; // 16bit
                            float[] samples = new float[samplesCount];
                            Debug.WriteLine($"Start process with {samplesCount} samples");
                            for (var i = 0; i < samplesCount; i++)
                            {
                                samples[i] = reader.ReadInt16() / 32768.0f; // 16bit
                            }
                            using var tempproc = builder.Build();   // FIXME : keep one processor to avoid loosing context
                            tempproc.Process(samples);
                            memstream = new MemoryStream();
                        }
                    };
                    capture.RecordingStopped += (s, a) =>
                    {
                        capture.Dispose();
                    };
                    capture.StartRecording();
                    while (capture.CaptureState != NAudio.CoreAudioApi.CaptureState.Stopped)
                    {
                        Thread.Sleep(500);
                    }
                }
            });

        }
    }
}
