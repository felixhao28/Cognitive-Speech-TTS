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
using System.Threading;
using CognitiveServicesTTS;

namespace TTSSample
{
    internal class Program
    {
        private static Stopwatch sw = new Stopwatch();
        private static List<long> firstByteLatency = new List<long>();
        private static List<long> lastByteLatency = new List<long>();

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
            // For SoundPlayer to be able to play the wav file, it has to be encoded in PCM.
            // Use output audio format AudioOutputFormat.Riff16Khz16BitMonoPcm to do that.
            using (var memoryStream = new MemoryStream())
            {
                args.EventData.CopyTo(memoryStream);
                var x = memoryStream.ToArray();
                Console.WriteLine(x.Length + " bytes read.");
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

            foreach (var outputFormat in Enum.GetValues(typeof(AudioOutputFormat)).Cast<AudioOutputFormat>())
            {
                Console.WriteLine("AudioFormat = {0}", Enum.GetName(typeof(AudioOutputFormat), outputFormat));
                var count = 0;
                foreach (var text in sentences)
                {
                    Console.WriteLine("Test " + (++count));
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
                }
                firstByteLatency.RemoveAt(0);
                var avgFBL = firstByteLatency.Average();
                firstByteLatency.Clear();
                lastByteLatency.RemoveAt(0);
                var avgLBL = lastByteLatency.Average();
                lastByteLatency.Clear();
                Console.WriteLine("Average First Byte Latency: {0}ms", avgFBL);
                Console.WriteLine("Average Last Byte Latency: {0}ms", avgLBL);
            }
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

            Console.WriteLine("Starting TTSSample request code execution.");

            //LegacyTest(accessToken, sentences);

            HttpClientTest(accessToken, sentences);
        }
    }
}