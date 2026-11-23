// McBar — Creado por Andrés Londero
// 1.3.0 — Modo juego: mientras está prendido, McBar no molesta.
//  - La tecla Windows sola no abre el Launchpad ni el Inicio (en los juegos se aprieta sin querer).
//  - Las esquinas activas no hacen nada.
//  - Las notificaciones no aparecen en la isla (se guardan igual en el centro de notificaciones).
//  - El Dock se oculta solo (como con "Ocultación automática") y deja toda la pantalla.
// No se guarda en los ajustes: al reiniciar McBar arranca apagado (salvo que el perfil activo lo pida).
using System;

namespace McBar.Core
{
    public static class GameMode
    {
        public static bool On { get; private set; }

        public static event Action? Changed;

        public static void Set(bool on)
        {
            if (On == on) return;
            On = on;
            Log.Write(on ? "Modo juego: prendido" : "Modo juego: apagado");
            Changed?.Invoke();
        }

        public static void Toggle() => Set(!On);
    }
}
