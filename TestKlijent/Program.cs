using System;
using System.Net.Http;
using System.Threading.Tasks;

Console.WriteLine("Saljem 5 zahteva istovremeno za isti (nov) kljuc...");

HttpClient client = new HttpClient();
Task[] taskovi = new Task[5];

for (int i = 0; i < 5; i++)
{
    int brojZahteva = i;
    taskovi[i] = Task.Run(async () =>
    {
        var odgovor = await client.GetAsync("http://localhost:5051/slika3.jpg?prag=99");
        Console.WriteLine($"Zahtev {brojZahteva} zavrsen, status: {odgovor.StatusCode}");
    });
}

Task.WaitAll(taskovi);
Console.WriteLine("Svi zahtevi zavrseni.");