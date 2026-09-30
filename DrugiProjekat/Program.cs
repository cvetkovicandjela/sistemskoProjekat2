using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace drugiProjekat
{
    class Program
    {
        static HttpListener listener = new HttpListener();
        static Loger logger = new Loger("log.txt");
        static SlikaKes slikaKes = new SlikaKes(logger);
        static SemaphoreSlim semafor = new SemaphoreSlim(4, 4);

        static async Task Main(string[] args)
        {
            listener.Prefixes.Add("http://localhost:5051/");
            listener.Start();
            Console.WriteLine("Server je pokrenut na http://localhost:5051/");
            Console.WriteLine("Ceka se na zahteve...");

            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();

                //ne cekamo (await) obradu ovde, samo je pokrecemo da bi glavna petlja odmah mogla da prihvati sledeci zahtev
                _ = ObradiZahtevAsync(context);
            }
        }

        static async Task ObradiZahtevAsync(HttpListenerContext context)
        {
            await semafor.WaitAsync();
            HttpListenerResponse? response = null;
            string kljucKesa = "";

            try
            {
                HttpListenerRequest request = context.Request;
                response = context.Response;

                string nazivFajla = request.Url!.AbsolutePath.TrimStart('/');
                string? pragString = request.QueryString["prag"];

                logger.Zabelezi($"Zahtev za fajl: {nazivFajla}, prag: {pragString}");

                if (string.IsNullOrEmpty(nazivFajla) || string.IsNullOrEmpty(pragString) || !int.TryParse(pragString, out int prag))
                {
                    PosaljiGresku(response, "Nedostaje naziv fajla ili prag, ili prag nije validan broj.");
                    return;
                }

                string putanjaDoFajla = Path.Combine(Directory.GetCurrentDirectory(), nazivFajla);

                if (!File.Exists(putanjaDoFajla))
                {
                    PosaljiGresku(response, $"Fajl '{nazivFajla}' ne postoji na serveru.");
                    return;
                }

                kljucKesa = $"{nazivFajla}_{prag}";

                if (slikaKes.PokusajUzmiIliZauzmi(kljucKesa, out byte[] izKesa))
                {
                    await PosaljiSliku(response, izKesa);
                    return;
                }

                //Task.Run pokrece binarizaciju na pozadinskoj niti, a ContinueWith definise sta se radi kada se ta obrada zavrsi
                byte[] bajtoviSlike = await Task.Run(() => BinarizujSliku(putanjaDoFajla, prag))
                    .ContinueWith(zavrseniTask =>
                    {
                        if (zavrseniTask.IsFaulted)
                        {
                            throw zavrseniTask.Exception!.GetBaseException();
                        }

                        using Bitmap binarizovanaSlika = zavrseniTask.Result;
                        using MemoryStream memStream = new MemoryStream();
                        binarizovanaSlika.Save(memStream, ImageFormat.Png);
                        byte[] rezultat = memStream.ToArray();

                        slikaKes.DodajIOslobodi(kljucKesa, rezultat);
                        logger.Zabelezi($"Binarizacija zavrsena za kljuc: {kljucKesa}");

                        return rezultat;
                    });

                await PosaljiSliku(response, bajtoviSlike);
            }
            catch (Exception e)
            {
                if (!string.IsNullOrEmpty(kljucKesa))
                    slikaKes.OslobodiBezUpisa(kljucKesa);

                logger.Zabelezi($"[GRESKA] Neocekivana greska prilikom obrade zahteva: {e.Message}");

                try
                {
                    if (response != null)
                        PosaljiGresku(response, "Doslo je do neocekivane greske na serveru.");
                }
                catch { }
            }
            finally
            {
                semafor.Release();
            }
        }

        static Bitmap BinarizujSliku(string putanjaDoFajla, int prag)
        {
            using (Bitmap originalna = new Bitmap(putanjaDoFajla))
            {
                Bitmap rezultat = new Bitmap(originalna.Width, originalna.Height);

                for (int x = 0; x < originalna.Width; x++)
                {
                    for (int y = 0; y < originalna.Height; y++)
                    {
                        Color piksel = originalna.GetPixel(x, y);
                        int prosek = (piksel.R + piksel.G + piksel.B) / 3;

                        if (prosek < prag)
                            rezultat.SetPixel(x, y, Color.Black);
                        else
                            rezultat.SetPixel(x, y, Color.White);
                    }
                }

                return rezultat;
            }
        }

        static async Task PosaljiSliku(HttpListenerResponse response, byte[] podaci)
        {
            response.ContentType = "image/png";
            response.ContentLength64 = podaci.Length;
            await response.OutputStream.WriteAsync(podaci, 0, podaci.Length);
            response.OutputStream.Close();
        }

        static void PosaljiGresku(HttpListenerResponse response, string poruka)
        {
            response.StatusCode = 404;
            byte[] bajtovi = System.Text.Encoding.UTF8.GetBytes(poruka);
            response.ContentLength64 = bajtovi.Length;
            response.OutputStream.Write(bajtovi, 0, bajtovi.Length);
            response.OutputStream.Close();
        }
    }
}