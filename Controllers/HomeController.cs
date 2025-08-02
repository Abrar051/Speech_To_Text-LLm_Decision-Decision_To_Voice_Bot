using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using VoiceBot.Models;
using NAudio.Lame;
using NAudio.Wave;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace VoiceBot.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;

        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }
        public IActionResult SpeechConverted()
        {
            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        //public async Task<IActionResult> UploadWav(IFormFile file)
        //{
        //    if (file == null || file.Length == 0)
        //        return Json(new { success = false, error = "No file received" });

        //    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        //    Directory.CreateDirectory(uploadsPath);

        //    var wavPath = Path.Combine(uploadsPath, "recording.wav");
        //    var mp3Path = Path.Combine(uploadsPath, "recording.mp3");

        //    using (var stream = new FileStream(wavPath, FileMode.Create))
        //    {
        //        await file.CopyToAsync(stream);
        //    }

        //    try
        //    {
        //        using (var reader = new NAudio.Wave.AudioFileReader(wavPath))
        //        using (var writer = new NAudio.Lame.LameMP3FileWriter(mp3Path, reader.WaveFormat, 128))
        //        {
        //            reader.CopyTo(writer);
        //        }

        //        var mp3Url = Url.Content("~/uploads/recording.mp3");
        //        return Json(new { success = true, mp3Url });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { success = false, error = ex.Message });
        //    }
        //}
        public async Task<IActionResult> UploadWav(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, error = "No file received" });

            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsPath);

            var wavPath = Path.Combine(uploadsPath, "recording.wav");

            using (var wavStream = new FileStream(wavPath, FileMode.Create))
            {
                await file.CopyToAsync(wavStream);
            }

            try
            {
                using var mp3MemoryStream = new MemoryStream();
                using (var reader = new AudioFileReader(wavPath))
                using (var writer = new LameMP3FileWriter(mp3MemoryStream, reader.WaveFormat, 128))
                {
                    reader.CopyTo(writer);
                }

                mp3MemoryStream.Position = 0;

                var groqApiKey = _configuration["GroqApi:ApiKey"];
                if (string.IsNullOrEmpty(groqApiKey))
                    return Json(new { success = false, error = "Groq API key is not configured" });

                using var client = new HttpClient();

                var requestContent = new MultipartFormDataContent();
                requestContent.Add(new StringContent("whisper-large-v3-turbo"), "model");
                requestContent.Add(new StreamContent(mp3MemoryStream), "file", "recording.mp3");
                requestContent.Add(new StringContent("verbose_json"), "response_format");

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", groqApiKey);
                request.Content = requestContent;

                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, error = $"Groq API error: {errorText}" });
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();

                var transcriptionResult = JsonConvert.DeserializeObject<TranscriptionResponse>(jsonResponse);

                ///
                var chatPrompt = transcriptionResult.Text;

                var chatRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
                chatRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", groqApiKey);

                var chatPayload = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = "You are a helpful agent who helps the user by answering their query within 5 sentence" },
                        new { role = "user", content = chatPrompt }
                    },
                    model = "meta-llama/llama-4-maverick-17b-128e-instruct",
                    temperature = 1,
                    max_completion_tokens = 1024,
                    top_p = 1,
                    stream = false,
                    stop = (string)null
                };
                var chatJson = JsonConvert.SerializeObject(chatPayload);
                chatRequest.Content = new StringContent(chatJson, System.Text.Encoding.UTF8, "application/json");
                var chatResponse = await client.SendAsync(chatRequest);
                var chatContent = await chatResponse.Content.ReadAsStringAsync();
                var groqResult = JsonConvert.DeserializeObject<GroqChatResponse>(chatContent);
                var assistantMessage = groqResult?.Choices?.FirstOrDefault()?.Message?.Content;
                ///
                if (string.IsNullOrWhiteSpace(assistantMessage))
                    return Json(new { success = false, error = "No response from Groq chat." });
                if (!string.IsNullOrEmpty(assistantMessage))
                {
                    // Remove all ** and " characters
                    assistantMessage = assistantMessage.Replace("**", "").Replace("\"", "");
                }
                //
                int maxLength = 500; 
                if (!string.IsNullOrEmpty(assistantMessage) && assistantMessage.Length > maxLength)
                {
                    assistantMessage = assistantMessage.Substring(0, maxLength);
                }

                //
                // Text-to-Speech Request
                var ttsPayload = new
                {
                    model = "playai-tts",
                    voice = "Jennifer-PlayAI",
                    input = assistantMessage,
                    response_format = "wav"
                };
                ///
                System.IO.File.Delete(wavPath);
                var ttsRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/speech")
                {
                    Headers = { Authorization = new AuthenticationHeaderValue("Bearer", groqApiKey) },
                    Content = new StringContent(JsonConvert.SerializeObject(ttsPayload), Encoding.UTF8, "application/json")
                };

                var ttsResponse = await client.SendAsync(ttsRequest);
                if (!ttsResponse.IsSuccessStatusCode)
                {
                    var error = await ttsResponse.Content.ReadAsStringAsync();
                    return Json(new { success = false, error = $"Groq TTS error: {error}" });
                }

                var ttsAudio = await ttsResponse.Content.ReadAsByteArrayAsync();

                System.IO.File.Delete(wavPath);
                //return File(ttsAudio, "audio/wav", "response.wav");
                var savedFileName = "GroqResponse.mp3";
                var savedFilePath = Path.Combine(uploadsPath, savedFileName);

                await System.IO.File.WriteAllBytesAsync(savedFilePath, ttsAudio);

                return File(System.IO.File.ReadAllBytes(savedFilePath), "audio/wav", savedFileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
