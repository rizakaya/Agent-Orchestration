# AgentToolGateway.Api

Bu proje, **agent tool calling**, **controlled tool execution**, **approval flow** ve **audit logging** kavramlarını küçük ve anlaşılır bir ASP.NET Core Minimal API üzerinden öğrenmek için hazırlanmış bir demodur.

Uygulama bir ajan veya LLM tarafından seçilen tool çağrısını alır, bu tool'un gerçekten kayıtlı olup olmadığını kontrol eder, gelen JSON input'u ilgili C# modele dönüştürür, risk seviyesine göre onay gerekip gerekmediğine bakar, tool'u timeout ile çalıştırır ve yapılan işlemi audit log olarak kaydeder.

## Amaç

Bir ajan sistemi kurduğumuzda model sadece cevap üretmez. Bazen uygulama adına işlem yapmak ister:

```text
Kodda ToolDefinition geçen yerleri ara.
```

veya:

```text
Program.cs dosyasını oku.
```

veya daha riskli bir şekilde:

```text
dotnet test komutunu çalıştır.
```

Bu isteklerin hepsi aynı güven seviyesinde değildir. Kod aramak ve dosya okumak düşük riskli olabilir. Ama komut çalıştırmak veya dosya değiştirmek daha dikkatli yönetilmelidir.

Bu projenin amacı şunu göstermektir:

```text
Ajan isteği
 -> Tool intent
 -> Gateway endpoint
 -> Tool registry
 -> Policy kontrolü
 -> Gerekirse approval
 -> Tool execution
 -> Audit log
 -> Tool sonucu
```

Yani ajan güçlü araçları doğrudan çalıştırmaz. Önce bu gateway'e gelir. Gateway, "bu araç var mı, bu input doğru mu, bu işlem güvenli mi, onay gerekiyor mu?" sorularını cevaplar.

## Neyi Öğrendik?

Bu demo ile şu konuları pratik olarak görmüş olduk:

- Minimal API ile agent tool gateway oluşturma
- Tool'ları standart bir interface arkasında toplama
- Her tool için ad, açıklama, risk seviyesi, erişim tipi ve timeout tanımlama
- JSON input'u tool'un beklediği typed C# modele dönüştürme
- Tool çağrısını merkezi bir `ToolExecutor` üzerinden çalıştırma
- Düşük, orta ve yüksek riskli işlemleri ayırma
- Yüksek riskli işlemler için approval token isteme
- Komut çalıştırırken whitelist kullanma
- Komut ve tool çalıştırmalarına timeout uygulama
- Dosya erişimini workspace sınırları içinde tutma
- Her tool çağrısını JSONL audit log olarak kaydetme
- Ollama/Qwen ile doğal dil isteğini structured tool intent'e dönüştürme
- Modelin karar verdiği, uygulamanın ise güvenli şekilde çalıştırdığı mimariyi kurma

## Temel Kavramlar

### Agent Tool Gateway

Agent Tool Gateway, ajan ile sistem araçları arasındaki güvenli geçiş noktasıdır.

Ajan şöyle bir şey yapmak isteyebilir:

```text
AgentToolGateway.Api/Program.cs dosyasını oku.
```

Bu istek doğrudan dosya sistemine gitmez. Gateway'e şu şekilde gelir:

```json
{
  "runId": "agent-run-001",
  "toolName": "ReadFile",
  "input": {
    "path": "AgentToolGateway.Api/Program.cs"
  }
}
```

Gateway bu isteği alır ve şu kontrolleri yapar:

- `ReadFile` adında kayıtlı bir tool var mı?
- Gelen input, `ReadFileInput` modeline çevrilebiliyor mu?
- Okunmak istenen dosya workspace içinde mi?
- Bu tool approval istiyor mu?
- Tool belirlenen süre içinde çalışıyor mu?

Bu sayede ajanların araç kullanımı tek bir kontrollü hat üzerinden yönetilir.

### Tool Definition

Her tool kendini bir `ToolDefinition` ile tanımlar.

Örneğin `RunCommand` tool'u şöyle tanımlanır:

```csharp
public override ToolDefinition Definition { get; } = new()
{
    Name = "RunCommand",
    Description = "Runs an approval-required whitelisted command.",
    RiskLevel = ToolRiskLevel.High,
    AccessType = ToolAccessType.Execute,
    RequiresApproval = true,
    TimeoutSeconds = 30
};
```

Bu tanım gateway'e şunları söyler:

- Tool'un adı nedir?
- Tool ne iş yapar?
- Risk seviyesi nedir?
- Okuma, yazma veya komut çalıştırma mı yapar?
- Kullanıcı onayı gerekir mi?
- En fazla kaç saniye çalışabilir?
- Audit log yazılacak mı?

Bu yapı sayesinde tool'lar sadece fonksiyon değil, aynı zamanda güvenlik bilgisi taşıyan kontrollü yetenekler haline gelir.

### Tool Calling

Tool calling, ajanın seçtiği aracı uygulama tarafında çalıştırma sürecidir.

Bu projede tool'lar doğrudan endpoint içinde yazılmaz. Hepsi `IAgentTool` standardına uyar:

```csharp
public interface IAgentTool
{
    ToolDefinition Definition { get; }
    Type InputType { get; }
    Task<object?> ExecuteAsync(object input, CancellationToken cancellationToken);
}
```

Typed tool yazmak için de `AgentTool<TInput, TOutput>` base class'ı kullanılır.

Buradaki önemli fikir şudur:

```text
Model veya ajan tool'u seçer.
Gateway tool'u güvenli şekilde çalıştırır.
```

Yani model doğrudan dosya okumaz, komut çalıştırmaz veya patch uygulamaz. Bunları uygulama kodu, belirlenmiş kurallar içinde yapar.

### Policy ve Approval

Her tool aynı riskte değildir.

Bu projede tool risk seviyeleri şöyle ayrılır:

```text
Low      -> Düşük risk
Medium   -> Orta risk
High     -> Yüksek risk
Critical -> Kritik risk
```

Örneğin:

```text
SearchCode  -> Low
ReadFile    -> Low
GetGitDiff  -> Low
RunTests    -> Medium
RunCommand  -> High
ApplyPatch  -> High
```

`ToolPolicyEvaluator`, tool çalışmadan önce karar verir.

Eğer tool düşük riskliyse doğrudan çalışabilir.

Eğer tool yüksek riskliyse veya `RequiresApproval` true ise approval token gerekir.

Approval token yoksa gateway şu tarz bir hata döndürür:

```json
{
  "toolName": "RunCommand",
  "success": false,
  "error": {
    "code": "ApprovalRequired",
    "message": "Tool 'RunCommand' requires approval.",
    "approvalId": "approval-..."
  }
}
```

Bu cevap şunu anlatır:

```text
Bu tool çalıştırılmadı.
Önce kullanıcı veya sistem onayı gerekiyor.
```

Sonra approval token alınır ve aynı tool tekrar çağrılır.

### Audit Log

Audit log, ajanların ne yaptığını sonradan takip edebilmek için tutulur.

Bu projede tool çağrıları şu dosyaya JSONL formatında yazılır:

```text
AgentToolGateway.Api/audit/tool-calls.jsonl
```

JSONL formatında her satır ayrı bir kayıt anlamına gelir.

Bir audit kaydında şu bilgiler yer alır:

- `runId`
- `toolName`
- input
- risk seviyesi
- approval gerekip gerekmediği
- approval id
- başlama zamanı
- çalışma süresi
- başarılı olup olmadığı
- hata bilgisi

Bu bize şunu öğretir:

```text
Ajanlara tool vermek kadar,
ajanların tool kullanımını izlemek de önemlidir.
```

## Neden Böyle Bir Yapı Kurduk?

Basit bir demoda endpoint içine doğrudan kod yazılabilir:

```csharp
app.MapPost("/run-tests", () => ...)
```

Ama ajan tool sistemlerinde bu yaklaşım kısa sürede kontrolsüz hale gelir.

Çünkü ajan:

- Yanlış tool adı gönderebilir.
- Eksik veya hatalı input gönderebilir.
- Workspace dışındaki dosyaları okumaya çalışabilir.
- Uzun süren komutlar çalıştırabilir.
- Dosya değiştiren işlemler isteyebilir.
- Hangi işlemi ne zaman yaptığı sonradan incelenmek zorunda kalabilir.

Bu yüzden kodu sorumluluklarına göre ayırdık:

```text
AgentToolGateway.Api/
  Program.cs
  Abstractions/
    ToolAbstractions.cs
  Contracts/
    ToolContracts.cs
  Infrastructure/
    ToolRegistry.cs
    ToolExecutor.cs
    ToolPolicyEvaluator.cs
    InMemoryApprovalStore.cs
    JsonlToolAuditStore.cs
    ProcessRunner.cs
    WorkspacePathValidator.cs
    ToolGatewayOptions.cs
  Ollama/
    OllamaAdapter.cs
  Tools/
    SearchCodeTool.cs
    ReadFileTool.cs
    GetGitDiffTool.cs
    RunTestsTool.cs
    CreatePatchDraftTool.cs
    ApplyPatchTool.cs
    RunCommandTool.cs
```

Bu yapı sayesinde her class tek bir işle ilgilenir.

## Dosya ve Class Açıklamaları

### Program.cs

Uygulamanın başlangıç noktasıdır.

Görevleri:

- `ToolGateway` ayarlarını okumak
- `Ollama` ayarlarını okumak
- Infrastructure servislerini kaydetmek
- Tool'ları dependency injection'a eklemek
- Minimal API endpoint'lerini tanımlamak

Tool kayıtları burada yapılır:

```csharp
builder.Services.AddSingleton<IAgentTool, SearchCodeTool>();
builder.Services.AddSingleton<IAgentTool, ReadFileTool>();
builder.Services.AddSingleton<IAgentTool, GetGitDiffTool>();
builder.Services.AddSingleton<IAgentTool, RunTestsTool>();
builder.Services.AddSingleton<IAgentTool, CreatePatchDraftTool>();
builder.Services.AddSingleton<IAgentTool, ApplyPatchTool>();
builder.Services.AddSingleton<IAgentTool, RunCommandTool>();
```

Bu kayıtlar sayesinde `ToolRegistry` uygulamadaki bütün tool'ları görebilir.

Endpoint'ler de burada tanımlanır:

```csharp
app.MapGet("/api/tools", ...);
app.MapPost("/api/tool-calls", ...);
app.MapPost("/api/approvals", ...);
app.MapGet("/api/approvals/{approvalId}", ...);
app.MapPost("/api/agent-runs", ...);
```

### ToolContracts.cs

API'nin kullandığı veri modellerini içerir.

Önemli tipler:

- `ToolDefinition`: Tool metadata bilgisidir.
- `ToolRiskLevel`: Tool risk seviyesidir.
- `ToolAccessType`: Tool'un okuma, yazma, çalıştırma veya dış erişim türüdür.
- `ToolCallRequest`: `/api/tool-calls` endpoint'ine gelen istektir.
- `ToolExecutionResult`: Tool çalıştıktan sonra dönen standart sonuçtur.
- `ToolError`: Hata bilgisidir.
- `ToolAuditLogEntry`: Audit log'a yazılan kayıttır.
- `ApprovalRequest`: Approval oluşturma isteğidir.
- `ApprovalRecord`: Oluşturulan approval kaydıdır.
- `AgentPromptRequest`: Doğal dil prompt isteğidir.
- `AgentToolIntent`: Modelin seçtiği tool ve input bilgisidir.

Bu dosya gateway'in ortak dilini tanımlar.

### ToolAbstractions.cs

Tool sisteminin arayüzlerini ve base class'ını içerir.

Önemli parçalar:

- `IAgentTool`
- `IAgentTool<TInput, TOutput>`
- `AgentTool<TInput, TOutput>`
- `IToolRegistry`
- `IToolExecutor`
- `IToolPolicyEvaluator`
- `IToolAuditStore`
- `IApprovalStore`

Bu dosya sayesinde tool sistemi genişletilebilir hale gelir.

Yeni bir tool eklemek istediğimizde aynı standarda uyan yeni bir class yazmak yeterlidir.

### ToolRegistry.cs

Kayıtlı tool'ları tutar.

Görevleri:

- Tool adına göre tool bulmak
- Tool metadata listesini döndürmek
- Bilinmeyen tool çağrılırsa hata üretmek

`GET /api/tools` endpoint'i bu registry üzerinden çalışır.

### ToolExecutor.cs

Bu projenin merkezindeki class'tır.

Bir tool çağrısı geldiğinde asıl akışı `ToolExecutor` yönetir.

Görevleri:

- Tool adını registry'den bulmak
- JSON input'u ilgili C# input tipine çevirmek
- Policy kontrolü yapmak
- Approval gerekiyorsa çalıştırmayı durdurmak
- Timeout ayarlamak
- Tool'u çalıştırmak
- Başarılı sonuç veya hata sonucu üretmek
- Audit log yazmak

Bu class bize şunu öğretir:

```text
Tool çağırmak tek bir işlem değildir.
Tool çağırmak; doğrulama, izin, süre sınırı, hata yönetimi ve loglama içeren bir pipeline'dır.
```

### ToolPolicyEvaluator.cs

Tool'un çalıştırılıp çalıştırılamayacağına karar verir.

Mantığı şöyledir:

```text
Tool approval istemiyorsa ve yüksek riskli değilse çalışabilir.
Tool approval istiyorsa token kontrol edilir.
Token geçerliyse çalışabilir.
Token yoksa ApprovalRequired hatası döner.
```

Bu class güvenlik kararını tool'un kendisinden ayırır.

### InMemoryApprovalStore.cs

Approval kayıtlarını bellekte tutar.

Bir approval oluşturulduğunda:

- `approvalId` üretilir.
- `token` üretilir.
- `runId` ve `toolName` saklanır.
- 15 dakikalık geçerlilik süresi verilir.

Bu demo için bellekte tutmak yeterlidir. Gerçek bir üründe bu bilgiler veritabanında tutulmalıdır.

### JsonlToolAuditStore.cs

Tool çağrılarını JSONL dosyasına yazar.

Bu class, her tool çalışmasının izini bırakır.

Gerçek sistemlerde bu kayıtlar şuralara yazılabilir:

- Dosya
- Veritabanı
- Log yönetim sistemi
- Security audit sistemi

Bu projede öğrenmeyi kolaylaştırmak için JSONL dosyası kullanılır.

### WorkspacePathValidator.cs

Dosya erişimlerini workspace içinde tutar.

Örneğin ajan şöyle bir path göndermeye çalışırsa:

```text
../../secret.txt
```

`WorkspacePathValidator` bu yolu çözer ve workspace dışına çıkıyorsa hata üretir.

Bu class özellikle şu tool'lar için önemlidir:

- `SearchCode`
- `ReadFile`
- `ApplyPatch`

Bu bize şunu öğretir:

```text
Ajanın verdiği dosya yolu doğrudan güvenilir kabul edilmez.
Önce normalize edilir ve izin verilen kök klasör içinde mi diye kontrol edilir.
```

### ProcessRunner.cs

Komut çalıştırma işini yönetir.

Görevleri:

- Komutu workspace içinde çalıştırmak
- Windows'ta `cmd.exe /c`, diğer sistemlerde `/bin/sh -c` kullanmak
- Standard output okumak
- Standard error okumak
- Timeout süresi dolarsa process'i sonlandırmak

Bu class doğrudan public endpoint değildir.

Şu tool'lar tarafından kullanılır:

- `GetGitDiff`
- `RunTests`
- `RunCommand`

### OllamaAdapter.cs

Doğal dil isteğini structured tool intent'e dönüştürür.

Örneğin kullanıcı şöyle bir prompt gönderirse:

```text
Search the code for ToolDefinition
```

Ollama'dan şu yapıya benzer JSON istenir:

```json
{
  "toolName": "SearchCode",
  "input": {
    "query": "ToolDefinition"
  }
}
```

Burada model sadece tool seçer ve input üretir.

Tool'un gerçekten çalıştırılması yine `ToolExecutor` üzerinden yapılır.

## Endpoint Açıklamaları

### GET /api/tools

Kayıtlı tool listesini döndürür.

Bu endpoint ajan için tool kataloğu gibidir.

Örnek cevap mantığı:

```json
[
  {
    "name": "SearchCode",
    "description": "Searches text files in the workspace and returns matching snippets.",
    "riskLevel": "Low",
    "accessType": "Read",
    "requiresApproval": false,
    "timeoutSeconds": 10,
    "auditEnabled": true
  }
]
```

Ajan bu liste sayesinde hangi araçların mevcut olduğunu ve hangi aracın ne kadar riskli olduğunu görebilir.

### POST /api/tool-calls

Belirli bir tool'u çalıştırır.

Örnek istek:

```json
{
  "runId": "agent-run-001",
  "toolName": "SearchCode",
  "input": {
    "query": "ToolDefinition",
    "maxResults": 10
  }
}
```

Bu endpoint doğrudan tool çalıştırmaz. İsteği `ToolExecutor` class'ına verir.

Akış:

```text
ToolCallRequest
 -> ToolExecutor
 -> ToolRegistry
 -> ToolPolicyEvaluator
 -> IAgentTool
 -> ToolExecutionResult
```

### POST /api/approvals

Yüksek riskli tool'lar için approval kaydı oluşturur.

Örnek istek:

```json
{
  "runId": "agent-run-002",
  "toolName": "RunCommand",
  "input": {
    "command": "dotnet --info"
  },
  "reason": "Kullanıcı komut çalıştırılmasına izin verdi."
}
```

Örnek cevapta şunlar döner:

- `approvalId`
- `token`
- `runId`
- `toolName`
- `createdAt`
- `expiresAt`

Bu token daha sonra `/api/tool-calls` isteğinde `approvalToken` olarak gönderilir.

### GET /api/approvals/{approvalId}

Oluşturulmuş approval kaydını getirir.

Bu demo için approval bilgisini görmek amacıyla kullanılır.

Gerçek bir sistemde burada kullanıcı arayüzü, yetkilendirme ve onay ekranı olabilir.

### POST /api/agent-runs

Doğal dil prompt'unu alır, Ollama/Qwen ile tool intent'e çevirir ve sonra tool'u gateway üzerinden çalıştırır.

Örnek istek:

```json
{
  "runId": "agent-run-003",
  "prompt": "Search the code for ToolDefinition"
}
```

Akış:

```text
Prompt
 -> OllamaAdapter
 -> AgentToolIntent
 -> ToolExecutor
 -> Tool result
```

Bu endpoint, structured output fikrinin gateway tarafındaki karşılığıdır.

Model serbest metni şu yapıya dönüştürür:

```json
{
  "toolName": "SearchCode",
  "input": {
    "query": "ToolDefinition"
  }
}
```

Sonra uygulama bu structured intent'i güvenli şekilde çalıştırır.

## Tool Açıklamaları

### SearchCode

Workspace içindeki metin dosyalarında arama yapar.

Input modeli:

```json
{
  "query": "ToolDefinition",
  "path": null,
  "maxResults": 20
}
```

Desteklenen dosya türleri:

- `.cs`
- `.json`
- `.md`
- `.txt`
- `.csproj`
- `.sln`
- `.props`
- `.targets`

Bu tool düşük risklidir çünkü sadece okuma yapar.

### ReadFile

Workspace içindeki bir dosyayı okur.

Örnek input:

```json
{
  "path": "AgentToolGateway.Api/Program.cs"
}
```

Dosya yolu önce `WorkspacePathValidator` ile kontrol edilir.

Bu tool düşük risklidir ve approval gerektirmez.

### GetGitDiff

Workspace içindeki mevcut git diff bilgisini döndürür.

Örnek input:

```json
{
  "stagedOnly": false
}
```

`stagedOnly` false ise:

```text
git diff
```

`stagedOnly` true ise:

```text
git diff --cached
```

çalıştırılır.

Bu tool okuma amaçlıdır.

### RunTests

Whitelist içinde izin verilen test komutunu çalıştırır.

Örnek input:

```json
{
  "command": "dotnet test",
  "timeoutSeconds": 60
}
```

Komut verilmezse varsayılan olarak `dotnet test` kullanılır.

Bu tool orta risklidir. Approval istemez ama sadece izin verilen komutları çalıştırır.

### CreatePatchDraft

Patch taslağı oluşturur ama dosya değiştirmez.

Örnek input:

```json
{
  "patch": "*** Begin Patch\n*** End Patch"
}
```

Bu tool'un amacı, dosya değişikliği yapmadan önce patch içeriğini taşınabilir bir taslak haline getirmektir.

### ApplyPatch

Patch uygulama niyetini temsil eder.

Bu v1 sürümünde gerçek dosya değişikliği yapmaz. Dry-run mock olarak çalışır.

Örnek cevap:

```json
{
  "applied": false,
  "dryRun": true,
  "message": "Patch was approved by policy but not applied because v1 runs ApplyPatch as a dry-run mock."
}
```

Yine de yüksek riskli kabul edilir ve approval ister. Çünkü gerçek bir üründe bu tool dosya değiştirebilir.

### RunCommand

Whitelist içindeki bir komutu çalıştırır.

Örnek input:

```json
{
  "command": "dotnet --info",
  "timeoutSeconds": 30
}
```

Bu tool yüksek risklidir ve approval ister.

Komut ayrıca `AllowedRunCommands` listesinde olmalıdır.

## Uygulama Nasıl Çalışır?

Tool çağrısı adım adım şöyle işler:

1. İstemci `/api/tool-calls` endpoint'ine istek gönderir.
2. `Program.cs`, isteği `ToolExecutor` class'ına verir.
3. `ToolExecutor`, tool adını `ToolRegistry` ile çözer.
4. Tool bulunamazsa `UnknownTool` hatası döner.
5. Input JSON'u tool'un beklediği input modeline dönüştürülür.
6. JSON hatalıysa `InvalidInput` hatası döner.
7. `ToolPolicyEvaluator`, tool'un risk seviyesine bakar.
8. Approval gerekiyorsa token kontrol edilir.
9. Token yoksa veya geçersizse `ApprovalRequired` hatası döner.
10. İzin varsa tool timeout ile çalıştırılır.
11. Tool süresi aşarsa `Timeout` hatası döner.
12. Tool başarılıysa output döner.
13. Başarılı veya hatalı her çağrı audit log'a yazılır.

Bu akış bize şunu gösterir:

```text
Güvenli tool calling, sadece fonksiyon çağırmak değildir.
Doğrulama, izin, çalıştırma, hata yönetimi ve izleme birlikte düşünülmelidir.
```

## Approval Akışı

Yüksek riskli bir tool'u approval token olmadan çağırırsak:

```json
{
  "runId": "agent-run-004",
  "toolName": "RunCommand",
  "input": {
    "command": "dotnet --info"
  }
}
```

Gateway tool'u çalıştırmaz ve şuna benzer hata döndürür:

```json
{
  "toolName": "RunCommand",
  "success": false,
  "error": {
    "code": "ApprovalRequired",
    "message": "Tool 'RunCommand' requires approval.",
    "approvalId": "approval-..."
  }
}
```

Sonra approval oluşturulur:

```json
{
  "runId": "agent-run-004",
  "toolName": "RunCommand",
  "input": {
    "command": "dotnet --info"
  },
  "reason": "Komut çalıştırmak için kullanıcı onayı alındı."
}
```

Approval cevabındaki `token` ile tool tekrar çağrılır:

```json
{
  "runId": "agent-run-004",
  "toolName": "RunCommand",
  "approvalToken": "TOKEN_DEGERI",
  "input": {
    "command": "dotnet --info"
  }
}
```

Bu sefer token geçerliyse tool çalıştırılır.

## Gereksinimler

Bu uygulamayı çalıştırmak için şunlar gerekir:

- .NET 8 SDK
- Ollama
- Yerel olarak indirilmiş bir model
- Varsayılan olarak `qwen2.5-coder` modeli
- Ollama servisinin çalışıyor olması

Ollama sadece `/api/agent-runs` endpoint'i için gereklidir.

`/api/tools`, `/api/tool-calls` ve `/api/approvals` endpoint'leri doğrudan API mantığıyla çalışır.

## Ollama Hazırlığı

Önce Ollama'nın çalıştığından emin ol:

```powershell
ollama serve
```

Model yüklü değilse indir:

```powershell
ollama pull qwen2.5-coder
```

Ollama ayarları `appsettings.json` içindedir:

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen2.5-coder"
  }
}
```

## Nasıl Çalıştırılır?

Proje klasörüne git:

```powershell
cd "C:\projects\Lessons\Structured output\StructuredAssistant.Demo"
```

Uygulamayı çalıştır:

```powershell
dotnet run --project .\AgentToolGateway.Api\AgentToolGateway.Api.csproj
```

Endpoint örneklerini `AgentToolGateway.Api/AgentToolGateway.Api.http` dosyasından deneyebilirsin.

## Kullanım Örnekleri

### 1. Tool Listesini Görme

İstek:

```http
GET /api/tools
```

Beklenen fikir:

```json
[
  {
    "name": "SearchCode",
    "riskLevel": "Low",
    "accessType": "Read",
    "requiresApproval": false
  }
]
```

Bu çağrı ajan için mevcut tool kataloğunu verir.

### 2. Kod İçinde Arama Yapma

İstek:

```json
{
  "runId": "agent-run-001",
  "toolName": "SearchCode",
  "input": {
    "query": "ToolDefinition",
    "maxResults": 10
  }
}
```

Çalışacak tool:

```text
SearchCode
```

Beklenen sonuç:

```json
{
  "toolName": "SearchCode",
  "success": true,
  "output": {
    "matches": []
  }
}
```

Eşleşme varsa dosya yolu, satır numarası ve kısa snippet döner.

### 3. Dosya Okuma

İstek:

```json
{
  "runId": "agent-run-002",
  "toolName": "ReadFile",
  "input": {
    "path": "AgentToolGateway.Api/Program.cs"
  }
}
```

Çalışacak tool:

```text
ReadFile
```

Bu çağrı düşük risklidir ve approval istemez.

### 4. Test Çalıştırma

İstek:

```json
{
  "runId": "agent-run-003",
  "toolName": "RunTests",
  "input": {
    "command": "dotnet test",
    "timeoutSeconds": 60
  }
}
```

Çalışacak tool:

```text
RunTests
```

Bu tool sadece whitelist içindeki test komutlarını çalıştırır.

### 5. Approval Gerektiren Komut

İlk istek:

```json
{
  "runId": "agent-run-004",
  "toolName": "RunCommand",
  "input": {
    "command": "dotnet --info"
  }
}
```

Beklenen fikir:

```text
Gateway bu isteği hemen çalıştırmaz.
Önce approval ister.
```

Token alındıktan sonra tekrar çağrılır:

```json
{
  "runId": "agent-run-004",
  "toolName": "RunCommand",
  "approvalToken": "TOKEN_DEGERI",
  "input": {
    "command": "dotnet --info"
  }
}
```

### 6. Patch Taslağı Oluşturma

İstek:

```json
{
  "runId": "agent-run-005",
  "toolName": "CreatePatchDraft",
  "input": {
    "patch": "*** Begin Patch\n*** End Patch"
  }
}
```

Çalışacak tool:

```text
CreatePatchDraft
```

Bu tool dosya değiştirmez. Sadece patch taslağı oluşturur.

### 7. Doğal Dil ile Tool Seçtirme

İstek:

```json
{
  "runId": "agent-run-006",
  "prompt": "Search the code for ToolDefinition"
}
```

Beklenen akış:

```text
Prompt
 -> OllamaAdapter
 -> SearchCode intent
 -> ToolExecutor
 -> SearchCode sonucu
```

Bu örnek, structured output fikrinin API tarafındaki karşılığıdır.

## appsettings.json

Uygulama ayarları şu dosyadan gelir:

```json
{
  "ToolGateway": {
    "WorkspaceRoot": "..",
    "AuditLogPath": "audit/tool-calls.jsonl",
    "AllowedTestCommands": [
      "dotnet test"
    ],
    "AllowedRunCommands": [
      "dotnet --info",
      "dotnet test"
    ]
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen2.5-coder"
  }
}
```

Önemli alanlar:

- `WorkspaceRoot`: Tool'ların erişebileceği kök klasör.
- `AuditLogPath`: Tool çağrılarının yazılacağı JSONL dosyası.
- `AllowedTestCommands`: `RunTests` için izin verilen komutlar.
- `AllowedRunCommands`: `RunCommand` için izin verilen komutlar.
- `Ollama.BaseUrl`: Ollama servis adresi.
- `Ollama.Model`: Tool intent üretmek için kullanılacak model.

## Önemli Notlar

Bu proje eğitim amaçlıdır.

`ApplyPatch` gerçek dosya değişikliği yapmaz. V1 sürümünde dry-run mock olarak çalışır.

`InMemoryApprovalStore` approval kayıtlarını bellekte tutar. Uygulama kapanınca kayıtlar kaybolur.

`RunCommand` ve `RunTests` sadece whitelist içindeki komutları çalıştırır.

Dosya erişimi `WorkspacePathValidator` ile workspace içinde tutulur.

Audit kayıtları JSONL dosyasına yazılır. Bu dosya demo sırasında oluşan tool çağrılarını takip etmek için kullanılabilir.

Gerçek bir ürüne dönüştürmek istersen şu geliştirmeler yapılabilir:

- Approval kayıtlarını veritabanında tutma
- Kullanıcı bazlı yetkilendirme
- Tool bazlı rol ve izin sistemi
- Gerçek patch uygulama ve rollback desteği
- Daha güçlü JSON schema doğrulaması
- Audit log için arama ve filtreleme ekranı
- Tool bazlı rate limit
- Kullanıcı veya ajan kimliği ile loglama
- Web arayüzü üzerinden approval yönetimi
- Testler

## Kısa Özet

Bu uygulama şunu öğretir:

```text
Ajanlara güçlü araçlar verilebilir,
ama bu araçlar güvenli bir gateway üzerinden yönetilmelidir.
```

Model doğal dili tool intent'e çevirebilir. Ama tool'u gerçekten çalıştıran taraf uygulama kodudur. Gateway tool'u bulur, input'u doğrular, riski değerlendirir, gerekirse onay ister, işlemi timeout ile çalıştırır ve sonucu audit log'a yazar.

Bu yüzden `AgentToolGateway.Api`, agent tool calling mimarisini, güvenlik kontrollerini, approval sürecini ve audit mantığını küçük ama anlaşılır bir Minimal API üzerinden gösteren bir örnektir.
