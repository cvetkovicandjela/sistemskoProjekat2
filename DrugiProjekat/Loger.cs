using System;
using System.IO;
using System.Threading;

namespace drugiProjekat
{
    class Loger
    {
        private object lokLog = new object();
        private string putanjaLogFajla;

        public Loger(string putanjaLogFajla)
        {
            this.putanjaLogFajla = putanjaLogFajla;
        }

        public void Zabelezi(string poruka)
        {
            string linija = $"{DateTime.Now:HH:mm:ss.fff} [nit {Thread.CurrentThread.ManagedThreadId}] {poruka}";

            lock (lokLog)
            {
                Console.WriteLine(linija);

                try
                {
                    File.AppendAllText(putanjaLogFajla, linija + Environment.NewLine);
                }
                catch (IOException)
                {
                    //fajl je trenutno nedostupan ali ne rusimo ceo server zbog toga
                }
                catch (UnauthorizedAccessException)
                {
                    //isto - ne prekidamo rad servera
                }
            }
        }
    }
}