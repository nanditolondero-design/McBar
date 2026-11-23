// McBar — Creado por Andrés Londero
// Pruebas de la etapa 11 (1.1.0): calendario iCal y recordatorios.
using System;
using System.Linq;
using McBar.Core;
using Xunit;

namespace McBar.Tests
{
    public class Stage11Tests
    {
        private const string Ics = "BEGIN:VCALENDAR\r\nX-WR-CALNAME:Trabajo\r\n" +
            "BEGIN:VEVENT\r\nSUMMARY:Turno dentista\r\nLOCATION:Consultorio\r\nDTSTART:20261125T093000\r\nDTEND:20261125T103000\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nSUMMARY:Feriado\r\nDTSTART;VALUE=DATE:20261208\r\nDTEND;VALUE=DATE:20261209\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nSUMMARY:Gimnasio\r\nDTSTART:20261123T190000\r\nDTEND:20261123T200000\r\nRRULE:FREQ=WEEKLY;BYDAY=MO,WE;COUNT=4\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nSUMMARY:Cancelado\r\nSTATUS:CANCELLED\r\nDTSTART:20261124T100000\r\nEND:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nSUMMARY:Reunión con\r\n  equipo\r\nDTSTART:20261126T150000\r\nEND:VEVENT\r\n" +
            "END:VCALENDAR\r\n";

        [Fact]
        public void Calendario_LeeEventosYRepeticiones()
        {
            var ev = IcsCalendar.Parse(Ics, new DateTime(2026, 11, 22), new DateTime(2026, 12, 31));
            var dentista = ev.Single(e => e.Title == "Turno dentista");
            Assert.Equal(new DateTime(2026, 11, 25, 9, 30, 0), dentista.Start);
            Assert.Equal("Consultorio", dentista.Location);
            Assert.Equal("Trabajo", dentista.Calendar);
            Assert.True(ev.Single(e => e.Title == "Feriado").AllDay);
            var gym = ev.Where(e => e.Title == "Gimnasio").Select(e => e.Start).ToList();
            Assert.Equal(new[] { new DateTime(2026, 11, 23, 19, 0, 0), new DateTime(2026, 11, 25, 19, 0, 0), new DateTime(2026, 11, 30, 19, 0, 0), new DateTime(2026, 12, 2, 19, 0, 0) }, gym);
            Assert.DoesNotContain(ev, e => e.Title == "Cancelado");
            Assert.Contains(ev, e => e.Title == "Reunión con equipo"); // renglón partido
        }

        [Fact]
        public void Calendario_HoraEnUtc()
        {
            var (local, allDay) = IcsCalendar.ParseDate("20261122T150000Z", null);
            Assert.False(allDay);
            Assert.Equal(new DateTime(2026, 11, 22, 15, 0, 0, DateTimeKind.Utc).ToLocalTime(), local);
        }

        private static readonly DateTime Now = new DateTime(2026, 11, 22, 21, 0, 0); // domingo

        [Fact]
        public void Recordatorios_Manana()
        {
            var (t, due) = Reminders.ParseQuick("comprar pan mañana 18:00", Now);
            Assert.Equal("Comprar pan", t);
            Assert.Equal(new DateTime(2026, 11, 23, 18, 0, 0), due);
        }

        [Fact]
        public void Recordatorios_EnMinutos()
        {
            var (t, due) = Reminders.ParseQuick("llamar a Juan en 20 min", Now);
            Assert.Equal("Llamar a Juan", t);
            Assert.Equal(Now.AddMinutes(20), due);
        }

        [Fact]
        public void Recordatorios_FechaYHora()
        {
            var (t, due) = Reminders.ParseQuick("turno dentista 25/11 9:30", Now);
            Assert.Equal("Turno dentista", t);
            Assert.Equal(new DateTime(2026, 11, 25, 9, 30, 0), due);
        }

        [Fact]
        public void Recordatorios_ALasYDiaDeSemana()
        {
            Assert.Equal(new DateTime(2026, 11, 22, 23, 0, 0), Reminders.ParseQuick("sacar la basura a las 23", Now).due);
            Assert.Equal(new DateTime(2026, 11, 23, 8, 0, 0), Reminders.ParseQuick("despertarme a las 8", Now).due); // ya pasó hoy: mañana
            Assert.Equal(new DateTime(2026, 11, 25, 9, 0, 0), Reminders.ParseQuick("pagar la luz el miércoles", Now).due);
        }

        [Fact]
        public void Recordatorios_SinHora()
        {
            var (t, due) = Reminders.ParseQuick("leer el libro", Now);
            Assert.Equal("Leer el libro", t);
            Assert.Null(due);
        }

        [Fact]
        public void Recordatorios_ComoSeMuestraLaHora()
        {
            Assert.Equal("hoy 18:00", Reminders.When(new DateTime(2026, 11, 22, 18, 0, 0), Now));
            Assert.Equal("mañana 09:30", Reminders.When(new DateTime(2026, 11, 23, 9, 30, 0), Now));
        }
    }
}
