//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
//
// Microsoft Cognitive Services (formerly Project Oxford): https://www.microsoft.com/cognitive-services
//
// Microsoft Cognitive Services (formerly Project Oxford) GitHub:
// https://github.com/Microsoft/Cognitive-Speech-TTS
//
// Copyright (c) Microsoft Corporation
// All rights reserved.
//
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CognitiveServicesTTS;

namespace TTSSample
{
    internal class Program
    {
        private static Stopwatch sw = new Stopwatch();
        private static List<long> firstByteLatency = new List<long>();
        private static List<long> lastByteLatency = new List<long>();
        private static List<int> bytesRead = new List<int>();
        private static string outputFilename = null;

        /// <summary>
        /// This method is called once the audio returned from the service.
        /// It will then attempt to play that audio file.
        /// Note that the playback will fail if the output audio format is not pcm encoded.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="GenericEventArgs{Stream}"/> instance containing the event data.</param>
        private static void PlayAudio(object sender, GenericEventArgs<Stream> args)
        {
            Console.WriteLine("First Byte Latency={0}", sw.ElapsedMilliseconds);
            firstByteLatency.Add(sw.ElapsedMilliseconds);
            using (var memoryStream = new MemoryStream())
            {
                args.EventData.CopyTo(memoryStream);
                var x = memoryStream.ToArray();
                Console.WriteLine(x.Length + " bytes read.");
                bytesRead.Add(x.Length);
                //if (outputFilename != null) File.WriteAllBytes(outputFilename, x);
                Console.WriteLine("Last Byte Latency={0}", sw.ElapsedMilliseconds);
                lastByteLatency.Add(sw.ElapsedMilliseconds);
            }
            args.EventData.Dispose();
        }

        /// <summary>
        /// Handler an error when a TTS request failed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="GenericEventArgs{Exception}"/> instance containing the event data.</param>
        private static void ErrorHandler(object sender, GenericEventArgs<Exception> e)
        {
            Console.WriteLine("Unable to complete the TTS request: [{0}]", e.EventData.ToString());
        }

        private static void HttpClientTest(string accessToken, string[] sentences)
        {
            string requestUri = "https://speech.platform.bing.com/synthesize";
            Console.WriteLine("===Http client code test===");
            var cortana = new Synthesize();

            cortana.OnAudioAvailable += PlayAudio;
            cortana.OnError += ErrorHandler;

            cortana.Speak(CancellationToken.None, new Synthesize.InputOptions()
            {
                RequestUri = new Uri(requestUri),
                // Text to speak
                Text = "Hello this is a warmup.",
                VoiceType = Gender.Female,
                // Refer to the documentation for complete list of supported locales.
                Locale = "en-US",
                // You can also customize the output voice. Refer to the documentation to view the different
                // voices that the TTS service can output.
                VoiceName = "Microsoft Server Speech Text to Speech Voice (en-US, ZiraRUS)",
                AuthorizationToken = "Bearer " + accessToken,
            }).Wait();

            double[] wavLength = new double[sentences.Length];
            var table = new StringBuilder("AudioFormat\tAvg. First Byte Latency\tAvg. Last Byte Latency\n");
            foreach (var outputFormat in Enum.GetValues(typeof(AudioOutputFormat)).Cast<AudioOutputFormat>())
            {
                var outputFormatName = Enum.GetName(typeof(AudioOutputFormat), outputFormat);
                Console.WriteLine("AudioFormat = {0}", outputFormatName);
                var count = 0;
                foreach (var text in sentences)
                {
                    Console.WriteLine("Test " + (++count));
                    outputFilename = String.Format("{0}-{1}", outputFormatName, count);
                    sw.Restart();
                    cortana.Speak(CancellationToken.None, new Synthesize.InputOptions()
                    {
                        RequestUri = new Uri(requestUri),
                        // Text to speak
                        Text = text,
                        VoiceType = Gender.Female,
                        // Refer to the documentation for complete list of supported locales.
                        Locale = "en-US",
                        // You can also customize the output voice. Refer to the documentation to view the different
                        // voices that the TTS service can output.
                        VoiceName = "Microsoft Server Speech Text to Speech Voice (en-US, ZiraRUS)",
                        // Service can return audio in different output format.
                        OutputFormat = outputFormat,
                        AuthorizationToken = "Bearer " + accessToken,
                    }).Wait();
                    if (outputFormat == AudioOutputFormat.Riff16Khz16BitMonoPcm)
                    {
                        wavLength[count - 1] = bytesRead[count - 1] / 32;
                    }
                }
                var avgFBL = firstByteLatency.Average();
                firstByteLatency.Clear();
                var avgLBL = lastByteLatency.Average();
                lastByteLatency.Clear();
                var avgBytes = bytesRead.Average();
                bytesRead.Clear();
                Console.WriteLine("Average First Byte Latency: {0} ms", avgFBL);
                Console.WriteLine("Average Last Byte Latency: {0} ms", avgLBL);
                table.AppendLine(outputFormatName + "\t" + avgFBL + "\t" + avgLBL);
            }
            Console.WriteLine(table);
            Console.WriteLine("Average Wav Length: {0} ms", wavLength.Average());
        }

        private static void Main(string[] args)
        {
            System.Net.HttpWebRequest.DefaultWebProxy = null;
            Console.WriteLine("Starting Authtentication");
            string accessToken;

            // Note: The way to get api key:
            // Free: https://www.microsoft.com/cognitive-services/en-us/subscriptions?productId=/products/Bing.Speech.Preview
            // Paid: https://portal.azure.com/#create/Microsoft.CognitiveServices/apitype/Bing.Speech/pricingtier/S0
            Authentication auth = new Authentication("Your api key goes here");

            try
            {
                accessToken = auth.GetAccessToken();
                Console.WriteLine("Token: {0}\n", accessToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed authentication.");
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.Message);
                return;
            }

            var sentences = File.ReadAllLines("en-US_SST1000.txt");
            //var sentences = new string[] { "Hello world." };

            Console.WriteLine("Starting TTSSample request code execution.");

            //LegacyTest(accessToken, sentences);

            HttpClientTest(accessToken, sentences);
        }
    }
}