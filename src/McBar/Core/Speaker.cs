// McBar — Creado por Andrés Londero
// 1.9.26: Leer en voz alta (como "Contenido hablado" de macOS). Con un atajo, McBar copia el texto seleccionado
// y lo lee con la voz de Windows. Volver a apretar el atajo lo corta. Usa la síntesis de voz de WinRT (sin
// librerías nuevas) y reproduce el audio con MediaPlayer.
using System;
using System.Threading.Tasks;
using System.Windows;
using McBar.Native;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace McBar.Core
{
    public static class Speaker
    {
        private static MediaPlayer? _player;

        /// <summary>¿Está leyendo algo ahora?</summary>
        public static bool IsSpeaking => _player != null;

        /// <summary>Si está leyendo, corta; si no, copia la selección y la lee.</summary>
        public static void Toggle()
        {
            if (IsSpeaking) { Stop(); return; }
            _ = SpeakSelectionAsync();
        }

        private static async Task SpeakSelectionAsync()
        {
            try
            {
                await Task.Delay(160); // dar tiempo a soltar Win+Mayús+L antes de mandar Ctrl+C
                // guardar el texto que hubiera en el portapapeles, para devolverlo después
                string? prev = null;
                try { if (Clipboard.ContainsText()) prev = Clipboard.GetText(); } catch { }

                WindowOps.SendChord(WindowOps.VK_CONTROL, 0x43); // Ctrl+C: copia la selección
                await Task.Delay(140);

                string? text = null;
                try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch { }

                // devolver el portapapeles como estaba (después de un ratito, así no pisa el copiado)
                if (prev != null) _ = RestoreClipboardAsync(prev);

                if (string.IsNullOrWhiteSpace(text))
                {
                    Notify("Leer en voz alta", "No hay texto seleccionado.");
                    return;
                }
                if (text.Length > 20000) text = text.Substring(0, 20000);
                await Speak(text);
            }
            catch (Exception ex) { Log.Write("Leer en voz alta: " + ex.Message); }
        }

        private static async Task Speak(string text)
        {
            Stop();
            var synth = new SpeechSynthesizer();
            try
            {
                var stream = await synth.SynthesizeTextToStreamAsync(text);
                var player = new MediaPlayer();
                player.MediaEnded += (_, _) => Stop();
                player.MediaFailed += (_, _) => Stop();
                player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
                _player = player;
                player.Play();
            }
            finally { synth.Dispose(); }
        }

        public static void Stop()
        {
            var p = _player; _player = null;
            if (p != null) { try { p.Pause(); p.Dispose(); } catch { } }
        }

        private static async Task RestoreClipboardAsync(string prev)
        {
            await Task.Delay(400);
            try { Clipboard.SetText(prev); } catch { }
        }

        private static void Notify(string title, string body)
        {
            try { NotificationCenter.Add(new NotificationInfo { App = "McBar", Title = title, Body = body }); } catch { }
        }
    }
}
