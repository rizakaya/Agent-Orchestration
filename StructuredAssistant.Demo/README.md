# StructuredAssistant.Demo

Bu proje, **structured output**, **tool calling** ve **human handoff** kavramlarını küçük ve anlaşılır bir konsol uygulaması üzerinden öğrenmek için hazırlanmış bir demodur.

Uygulama bir kullanıcı isteğini alır, yerel bir LLM modeliyle bu isteği yapılandırılmış JSON çıktısına dönüştürür, JSON alanlarına göre hangi işlemlerin yapılacağını seçer ve bazı durumlarda kullanıcıdan onay alır.

## Amaç

Normalde kullanıcılar serbest metin yazar:

```text
Yarın için 1 saatlik matematik ders planı hazırla, ücretsiz kaynaklar da öner.
```

Bu metin insanlar için anlaşılırdır ama uygulama kodu için doğrudan güvenilir değildir. Kodun karar verebilmesi için bu isteğin daha net alanlara ayrılması gerekir.

Bu projenin amacı şunu göstermektir:

```text
Serbest kullanıcı metni
 -> Structured output
 -> Intent routing
 -> Tool calling
 -> Gerekirse human handoff
 -> İşlem özeti
```

Yani model sadece cevap yazan bir sohbet botu gibi kullanılmaz. Model, uygulamanın anlayabileceği bir karar nesnesi üretir.

## Neyi Öğrendik?

Bu demo ile şu konuları pratik olarak görmüş olduk:

- Kullanıcı isteğini JSON formatında yapılandırılmış çıktıya dönüştürme
- Modelden beklenen cevap şeklini prompt içinde tarif etme
- Model çıktısını C# record modeline deserialize etme
- JSON alanlarına göre farklı araçları çalıştırma
- Eksik bilgi varsa kullanıcıya geri dönme
- Onay gerektiren işlemlerde human handoff kullanma
- Tüm kodu tek dosyada tutmak yerine sorumluluklara göre class'lara bölme

## Temel Kavramlar

### Structured Output

Structured output, modelin serbest metin yerine belirli bir şemaya uygun veri üretmesidir.

Bu projede modelden şu yapıya benzer bir JSON beklenir:

```json
{
  "Intent": "create_study_plan",
  "Topic": "matematik",
  "DurationMinutes": 60,
  "Date": "2026-06-29",
  "NeedsResources": true,
  "NeedsCalendar": false,
  "RequiresHumanApproval": false,
  "MissingFields": []
}
```

Bu çıktı sayesinde uygulama şunu anlayabilir:

- Kullanıcı ders planı istiyor mu?
- Konu ne?
- Süre belirtilmiş mi?
- Kaynak önerisi gerekiyor mu?
- Takvim işlemi gerekiyor mu?
- Kullanıcıdan onay alınmalı mı?
- Eksik alan var mı?

### Tool Calling

Tool calling, structured output içindeki alanlara göre uygulama tarafındaki fonksiyonların çalıştırılmasıdır.

Bu projede gerçek dış servisler yerine demo fonksiyonlar kullanılır:

- `CreateStudyPlan`: Ders planı oluşturur.
- `SearchFreeResources`: Ücretsiz kaynak önerir.
- `CreateCalendarDraft`: Takvim taslağı oluşturur.
- `SaveTask`: Görevi demo belleğine kaydetmiş gibi davranır.
- `AskHumanApproval`: Kullanıcıdan evet/hayır onayı alır.

Burada önemli fikir şudur: Model fonksiyonları doğrudan çalıştırmaz. Model sadece karar verilecek veriyi üretir. Fonksiyonları çalıştıran taraf uygulama kodudur.

### Human Handoff

Human handoff, uygulamanın bazı noktalarda kararı tekrar kullanıcıya bırakmasıdır.

Bu projede iki örnek vardır:

1. Eksik alan varsa kullanıcıya sorulur.
2. Takvim gibi onay gerektiren işlem varsa kullanıcıdan onay alınır.

Örneğin kullanıcı sadece şöyle yazarsa:

```text
matematik ders planı
```

Model süre bilgisinin eksik olduğunu söyler:

```json
"MissingFields": ["durationMinutes"]
```

Uygulama da kullanıcıya sorar:

```text
Eksik alanlar var (durationMinutes). Devam edilsin mi? (evet/hayır):
```

Kullanıcı `evet` derse varsayılan değerlerle devam edilir. Bu projede varsayılan süre 30 dakikadır.

## Neden Böyle Bir Yapı Kurduk?

İlk halinde tüm kod `Program.cs` içindeydi. Bu küçük demolar için çalışır ama konu büyüdükçe takip etmesi zorlaşır.

Bu yüzden kodu sorumluluklarına göre ayırdık:

```text
StructuredAssistant.Demo/
  Program.cs
  AssistantApp.cs
  Configuration/
    AppSettings.cs
  Models/
    AssistantIntent.cs
  Ollama/
    OllamaClient.cs
  Parsing/
    IntentParser.cs
  Routing/
    IntentRouter.cs
  Tools/
    DemoTools.cs
```

Bu yapı sayesinde her class tek bir işle ilgilenir.

## Dosya ve Class Açıklamaları

### Program.cs

Uygulamanın başlangıç noktasıdır. Nesneleri oluşturur ve uygulamayı başlatır.

```csharp
var settings = AppSettings.FromEnvironment();
var ollamaClient = new OllamaClient(settings);
var parser = new IntentParser(ollamaClient);
var tools = new DemoTools();
var router = new IntentRouter(tools);

var app = new AssistantApp(settings, parser, router);
await app.RunAsync();
```

Burada iş mantığı yoktur. Sadece uygulamanın parçaları birbirine bağlanır.

### AssistantApp.cs

Konsol uygulamasının ana döngüsünü yönetir.

Görevleri:

- Kullanıcıdan istek almak
- `q` yazılırsa çıkmak
- İsteği parser'a göndermek
- Structured output'u ekrana yazmak
- Router sonucunu ekrana yazmak
- Hata olursa kullanıcıya göstermek

### AppSettings.cs

Uygulama ayarlarını okur.

Varsayılan değerler:

- `OLLAMA_URL`: `http://localhost:11434`
- `OLLAMA_MODEL`: `qwen3.6:latest`

İstersen bu değerleri environment variable ile değiştirebilirsin.

### AssistantIntent.cs

Modelden beklediğimiz structured output tipidir.

Bu record, JSON çıktısının C# karşılığıdır:

```csharp
public sealed record AssistantIntent(
    string Intent,
    string Topic,
    int? DurationMinutes,
    string? Date,
    bool NeedsResources,
    bool NeedsCalendar,
    bool RequiresHumanApproval,
    string[] MissingFields);
```

### OllamaClient.cs

Ollama API ile konuşan class'tır.

Görevleri:

- Prompt'u Ollama'ya göndermek
- Model adını ayarlardan almak
- JSON formatında cevap istemek
- Model cevabını metin olarak geri döndürmek

Bu class sadece LLM bağlantısı ile ilgilenir.

### IntentParser.cs

Kullanıcı metnini structured output'a dönüştüren class'tır.

Görevleri:

- Prompt'u oluşturmak
- Modelden cevap almak
- Cevap içinden JSON kısmını ayıklamak
- JSON'u `AssistantIntent` nesnesine çevirmek
- `MissingFields` boş gelirse güvenli varsayılan değer vermek

Bu class, uygulamanın doğal dil anlama katmanıdır.

### IntentRouter.cs

Structured output'a göre hangi işlemlerin yapılacağına karar verir.

Örnek kararlar:

- `Intent` değeri `create_study_plan` değilse desteklenmeyen intent döndürür.
- `MissingFields` doluysa kullanıcıdan onay ister.
- `NeedsResources` true ise kaynak arama tool'unu çalıştırır.
- `NeedsCalendar` true ise takvim taslağı oluşturur.
- `RequiresHumanApproval` true ise kullanıcıdan ayrıca onay alır.

Bu class, tool calling mantığının merkezidir.

### DemoTools.cs

Demo amaçlı araçları içerir.

Gerçek bir uygulamada buradaki fonksiyonlar şunlara bağlanabilir:

- Veritabanı
- Google Calendar
- Outlook Calendar
- Dosya sistemi
- E-posta servisi
- Web arama servisi
- LMS veya eğitim platformu

Bu projede ise öğrenmeyi kolaylaştırmak için sadece metin döndürürler.

## Uygulama Nasıl Çalışır?

Akış adım adım şöyledir:

1. Kullanıcı konsola bir istek yazar.
2. `AssistantApp`, isteği `IntentParser` class'ına gönderir.
3. `IntentParser`, isteği prompt içine yerleştirir.
4. `OllamaClient`, prompt'u yerel modele gönderir.
5. Model JSON formatında structured output üretir.
6. JSON, `AssistantIntent` nesnesine çevrilir.
7. `AssistantApp`, structured output'u ekrana yazar.
8. `IntentRouter`, intent'e göre hangi tool'ların çalışacağını seçer.
9. `DemoTools`, ilgili işlemleri simüle eder.
10. İşlem özeti kullanıcıya gösterilir.

## Gereksinimler

Bu uygulamayı çalıştırmak için şunlar gerekir:

- .NET 8 SDK
- Ollama
- Yerel olarak indirilmiş bir model
- Varsayılan olarak `qwen3.6:latest` modeli
- Ollama servisinin çalışıyor olması

## Ollama Hazırlığı

Önce Ollama'nın çalıştığından emin ol:

```powershell
ollama serve
```

Model yüklü değilse indir:

```powershell
ollama pull qwen3.6:latest
```

Farklı bir model kullanmak istersen environment variable verebilirsin:

```powershell
$env:OLLAMA_MODEL="llama3.1:8b"
```

Ollama farklı adreste çalışıyorsa:

```powershell
$env:OLLAMA_URL="http://localhost:11434"
```

## Nasıl Çalıştırılır?

Proje klasörüne git:

```powershell
cd "C:\projects\Lessons\Structured output\StructuredAssistant.Demo"
```

Uygulamayı çalıştır:

```powershell
dotnet run --project .\StructuredAssistant.Demo
```

Çıkmak için:

```text
q
```

## Kullanım Örnekleri

### 1. Basit Ders Planı

Girdi:

```text
1 saatlik matematik ders planı hazırla
```

Beklenen fikir:

```json
{
  "Intent": "create_study_plan",
  "Topic": "matematik",
  "DurationMinutes": 60,
  "NeedsResources": false,
  "NeedsCalendar": false,
  "RequiresHumanApproval": false,
  "MissingFields": []
}
```

Çalışacak tool:

```text
CreateStudyPlan
SaveTask
```

### 2. Kaynak Önerisi İsteme

Girdi:

```text
45 dakikalık C# çalışma planı hazırla ve ücretsiz kaynaklar öner
```

Beklenen fikir:

```json
{
  "Intent": "create_study_plan",
  "Topic": "C#",
  "DurationMinutes": 45,
  "NeedsResources": true,
  "NeedsCalendar": false,
  "RequiresHumanApproval": false,
  "MissingFields": []
}
```

Çalışacak tool:

```text
CreateStudyPlan
SearchFreeResources
SaveTask
```

### 3. Eksik Bilgi ile Devam Etme

Girdi:

```text
matematik ders planı
```

Model süreyi eksik görebilir:

```json
"MissingFields": ["durationMinutes"]
```

Uygulama sorar:

```text
Eksik alanlar var (durationMinutes). Devam edilsin mi? (evet/hayır):
```

`evet` yazarsan uygulama varsayılan 30 dakika ile devam eder.

### 4. Takvim Taslağı

Girdi:

```text
yarın için 1 saatlik matematik ders planı hazırla ve takvim taslağı oluştur
```

Beklenen fikir:

```json
{
  "Intent": "create_study_plan",
  "Topic": "matematik",
  "DurationMinutes": 60,
  "NeedsCalendar": true
}
```

Çalışacak tool:

```text
CreateStudyPlan
CreateCalendarDraft
SaveTask
```

### 5. Onay Gerektiren Takvim İşlemi

Girdi:

```text
yarın 1 saatlik matematik ders planı hazırla, takvime eklemeden önce bana sor
```

Beklenen fikir:

```json
{
  "NeedsCalendar": true,
  "RequiresHumanApproval": true
}
```

Uygulama takvim taslağından sonra sorar:

```text
Bu işlem için onay gerekiyor: takvim taslağı kaydedilsin mi? (evet/hayır):
```

`evet` dersen devam eder, `hayır` dersen işlem handoff ile durur.

## Önemli Notlar

Bu proje eğitim amaçlıdır. Tool'lar gerçek servislerle konuşmaz, sadece simülasyon yapar.

Örneğin `CreateCalendarDraft` gerçek takvime kayıt atmaz. Sadece takvim taslağı oluşturulmuş gibi bir özet döndürür.

Gerçek bir ürüne dönüştürmek istersen şu geliştirmeler yapılabilir:

- Gerçek takvim API entegrasyonu
- Kalıcı veritabanı kaydı
- Daha güçlü JSON schema doğrulaması
- Eksik alanları tek tek kullanıcıdan tamamlama
- Daha fazla intent türü
- Testler
- Loglama
- Web API veya web arayüzü

## Kısa Özet

Bu uygulama şunu öğretir:

```text
LLM sadece cevap üretmek için değil,
uygulama kararlarını yapılandırılmış veriye çevirmek için de kullanılabilir.
```

Model doğal dili anlar. Uygulama structured output'u işler. Router doğru tool'ları seçer. Gerekirse kontrol tekrar insana verilir.

Bu yüzden bu demo, LLM destekli uygulama mimarisinin küçük ama anlaşılır bir örneğidir.
