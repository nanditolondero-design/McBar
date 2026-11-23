// McBar — Creado por Andrés Londero
// 1.8.0 — Posiciones de la pila en abanico del Dock (como macOS): cada archivo sube un poco más a la derecha
// y apenas más inclinado. Pura, se prueba.
namespace McBar.Core
{
    public static class StackFan
    {
        /// <summary>Corrimiento horizontal (DIP) y giro (grados) del elemento i (0 = el más cercano al Dock).</summary>
        public static (double dx, double angle) Position(int i)
        {
            if (i < 0) i = 0;
            return (0.9 * i * i, 1.1 * i);
        }
    }
}
