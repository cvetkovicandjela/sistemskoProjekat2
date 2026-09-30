using System;
using System.Collections.Generic;
using System.Threading;

namespace drugiProjekat
{
    class SlikaKes
    {
        private class StavkaKesa
        {
            public byte[] Podaci = Array.Empty<byte>();
            public DateTime VremeKreiranja;
        }

        private Dictionary<string, StavkaKesa> kes = new Dictionary<string, StavkaKesa>();
        private HashSet<string> uObradi = new HashSet<string>();
        private object lokKes = new object();
        private readonly TimeSpan vremeIsticanja = TimeSpan.FromSeconds(60);
        private Loger logger;

        //konstruktor prima logger da bi mogao da belezi dogadjaje u kesu
        public SlikaKes(Loger logger)
        {
            this.logger = logger;
        }

        public bool PokusajUzmiIliZauzmi(string kljuc, out byte[] podaci)
        {
            lock (lokKes)
            {
                while (true)
                {
                    if (kes.TryGetValue(kljuc, out StavkaKesa? stavka))
                    {
                        if (DateTime.Now - stavka.VremeKreiranja < vremeIsticanja)
                        {
                            podaci = stavka.Podaci;
                            logger.Zabelezi($"Pogodak u kesu za kljuc: {kljuc}");
                            return true;
                        }
                        else
                        {
                            kes.Remove(kljuc);
                        }
                    }

                    if (!uObradi.Contains(kljuc))
                    {
                        uObradi.Add(kljuc);
                        logger.Zabelezi($"Rezervisan kljuc za obradu: {kljuc}");
                        podaci = Array.Empty<byte>();
                        return false;
                    }

                    logger.Zabelezi($"Cekam da se zavrsi obrada za kljuc: {kljuc}");
                    Monitor.Wait(lokKes);
                }
            }
        }

        public void DodajIOslobodi(string kljuc, byte[] podaci)
        {
            lock (lokKes)
            {
                kes[kljuc] = new StavkaKesa
                {
                    Podaci = podaci,
                    VremeKreiranja = DateTime.Now
                };
                uObradi.Remove(kljuc);
                Monitor.PulseAll(lokKes);
            }
        }

        public void OslobodiBezUpisa(string kljuc)
        {
            lock (lokKes)
            {
                uObradi.Remove(kljuc);
                Monitor.PulseAll(lokKes);
            }
        }
    }
}