using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Konfiguracja Swaggera (¿eby mo¿na by³o testowaæ w przegl¹darce)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Dodajemy konwerter, ¿eby Enum z JSONa czyta³ siê jako tekst (np. "CSV"), a nie jako liczba
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// W³¹czenie interfejsu Swaggera
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/api/v1/parse-content", (ParseRequest request) =>
{
    // Sprawdzamy czy obiekt nie jest pusty
    if (request == null || request.Content == null || request.Content == "")
    {
        return Results.BadRequest(new { Status = "Error", Message = "Pole content jest puste." });
    }

    string decodedText = "";

    try
    {
        // Odkodowanie z Base64 do zwyklego stringa
        byte[] data = Convert.FromBase64String(request.Content);
        decodedText = Encoding.UTF8.GetString(data);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Status = "Error", Message = "Z³y format Base64: " + ex.Message });
    }

    try
    {
        // Wybieramy odpowiedni parser w zaleznosci od typu
        if (request.Type == PayloadType.CSV)
        {
            ParseResponse result = ParseCsv(decodedText);
            return Results.Ok(result);
        }
        else if (request.Type == PayloadType.INTERNAL_JSON)
        {
            ParseResponse result = ParseJson(decodedText);
            return Results.Ok(result);
        }
        else
        {
            return Results.BadRequest(new { Status = "Error", Message = "Nieobs³ugiwany typ danych." });
        }
    }
    catch (Exception)
    {
        return Results.BadRequest(new { Status = "Error", Message = "B³¹d podczas parsowania danych." });
    }
});

app.Run();

// Metoda parsuj¹ca CSV
static ParseResponse ParseCsv(string text)
{
    // Rozdzielamy tekst na linie
    string[] lines = text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

    if (lines.Length == 0)
    {
        ParseResponse emptyResponse = new ParseResponse();
        emptyResponse.Status = "Success";
        emptyResponse.ProcessedCount = 0;
        emptyResponse.Data = new List<object>();
        return emptyResponse;
    }

    // Pobieramy naglowki (pierwsza linia)
    string[] headers = lines[0].Split(',');
    List<object> list = new List<object>();

    // Lecimy petla po reszcie wierszy
    for (int i = 1; i < lines.Length; i++)
    {
        string[] values = lines[i].Split(',');
        Dictionary<string, string> row = new Dictionary<string, string>();

        for (int j = 0; j < headers.Length; j++)
        {
            // Zabezpieczenie przed brakiem wartosci w kolumnie
            if (j < values.Length)
            {
                row.Add(headers[j], values[j]);
            }
        }
        list.Add(row);
    }

    ParseResponse response = new ParseResponse();
    response.Status = "Success";
    response.ProcessedCount = list.Count;
    response.Data = list;

    return response;
}

// Metoda parsuj¹ca JSON
static ParseResponse ParseJson(string text)
{
    List<object> parsedList = new List<object>();

    try
    {
        // Najpierw próbujemy sparsowaæ jako listê obiektów
        parsedList = JsonSerializer.Deserialize<List<object>>(text);
    }
    catch
    {
        // Je¿eli rzuci b³¹d, to znaczy ¿e to pojedynczy obiekt, a nie tablica
        object singleObject = JsonSerializer.Deserialize<object>(text);
        parsedList.Add(singleObject);
    }

    ParseResponse response = new ParseResponse();
    response.Status = "Success";
    response.ProcessedCount = parsedList.Count;
    response.Data = parsedList;

    return response;
}

public enum PayloadType
{
    CSV,
    INTERNAL_JSON
}

public class ParseRequest
{
    public PayloadType Type { get; set; }
    public string Content { get; set; }
}

public class ParseResponse
{
    public string Status { get; set; }
    public int ProcessedCount { get; set; }
    public IEnumerable<object> Data { get; set; }
}