# Coding Conventions

**Analysis Date:** 2026-04-23

Tài liệu mô tả quy ước viết code trong DockLite, gồm hai nhánh: ứng dụng WPF/.NET 8 (C#) ở `src/` và service Go tại `wsl-docker-service/`. Ngôn ngữ mặc định cho bình luận và XML doc: tiếng Việt.

## Ngôn ngữ & target

**.NET (C#):**
- Target: `net8.0` cho thư viện, `net8.0-windows` cho WPF app (xem `src/DockLite.App/DockLite.App.csproj`, `src/DockLite.Core/DockLite.Core.csproj`).
- `<Nullable>enable</Nullable>` và `<ImplicitUsings>enable</ImplicitUsings>` bật trong mọi csproj (bao gồm `tests/DockLite.Tests/DockLite.Tests.csproj`).
- `Directory.Build.props` ở root chỉ khai báo `Version`/`Company`/`Product` — không bật analyzer, không khoá warning. Không có `.editorconfig` root nào đang được áp dụng (chỉ có `_file.editorconfig` được đổi tên trong `wsl-docker-service/` làm template cho sh LF).

**Go:**
- Module `docklite-wsl` (`wsl-docker-service/go.mod`), Go 1.25.0.
- Dependency chính: `github.com/docker/docker v26.1.5+incompatible`, `github.com/gorilla/websocket v1.5.3`. Không có framework web — dùng `net/http` thuần.
- Định dạng mặc định theo `gofmt` (tab indent, import tự sắp xếp). Tối thiểu bắt buộc: `go vet ./...` (chạy qua `wsl-docker-service/scripts/test-go.sh`).

## Quy ước đặt tên

### C#

**File và thư mục:**
- Một public type trên mỗi file; tên file trùng tên type: `WslPathNormalizer.cs`, `DockLiteApiClient.cs`, `ApiEnvelope.cs`.
- Partial class để tách nhóm lệnh/trạng thái lớn: `ContainersViewModel.cs` + `ContainersViewModel.BatchStats.cs`, `SettingsViewModel.cs` + `SettingsViewModel.WslCommands.cs`.
- Tên thư mục theo vai trò: `ViewModels/`, `Services/`, `Views/`, `Models/`, `Converters/`, `Behaviors/`, `Api/`, `Wsl/`, `Configuration/`, `Diagnostics/`, `Compose/`.

**Namespace:** file-scoped, khớp thư mục — ví dụ `namespace DockLite.App.Services;` trong `src/DockLite.App/Services/NetworkErrorMessageMapper.cs`.

**Type:**
- `PascalCase` cho class, struct, enum, record, interface.
- Interface có tiền tố `I`: `IAppShellFactory`, `IDockLiteApiClient`, `INotificationService`, `IDialogService`, `ICleanupScreenApi`.
- Lớp gần như luôn `sealed` khi không thiết kế để kế thừa: `public sealed class AppSettings`, `public sealed class ApiEnvelope<T>`, `public sealed class DockLiteApiClient`. Chỉ WPF `App`/cửa sổ/converter mới để không `sealed`.
- Lớp static-only: `public static class` (ví dụ `WslPathNormalizer`, `NetworkErrorMessageMapper`, `DockLiteErrorCodes`, `AppFileLog`, `ComposeComposePaths`).

**Thành viên:**
- Property, method: `PascalCase`.
- Private field: `_camelCase` có dấu gạch dưới (`_session`, `_composition`, `_hostContext`, `_notificationService`). Xem `src/DockLite.App/AppStartupCoordinator.cs`, `src/DockLite.Infrastructure/Api/DockLiteApiClient.cs`.
- Hằng số dùng `PascalCase` (không dùng `UPPER_SNAKE_CASE`): `private const string PrefixWslLocalhost = @"\\wsl.localhost\";`, `private const int MaxDepth = 5;` (`src/DockLite.Infrastructure/Wsl/WslPathNormalizer.cs`, `src/DockLite.App/Services/NetworkErrorMessageMapper.cs`).
- Hằng string cho mã domain cũng `PascalCase`: xem `src/DockLite.Contracts/Api/DockLiteErrorCodes.cs` (`Validation`, `NotFound`, `DockerUnavailable`, ...).
- Biến cục bộ: `camelCase`.

**Contract/DTO:**
- DTO API đặt trong `src/DockLite.Contracts/Api/` với hậu tố chỉ vai trò: `...Data` (body thành công), `...Request` (body input), `...Dto` (entity lồng trong data), `...Response` (cho `HealthResponse` trả trực tiếp, không bọc envelope).
- Dùng `[JsonPropertyName("...")]` để gắn tên JSON camelCase (xem `src/DockLite.Contracts/Api/ApiEnvelope.cs`).

### Go

**Package:**
- Đều chữ thường, không gạch dưới, một từ khi có thể: `apiresponse`, `appversion`, `dockerengine`, `httpserver`, `hostresources`, `wslimit`, `ws`, `compose`, `docker`, `settings`.
- Mỗi package có một file chứa doc comment cấp package ở dòng đầu, ví dụ:
  - `wsl-docker-service/internal/apiresponse/apiresponse.go`: `// Package apiresponse chuẩn hóa JSON { success, data?, error? } cho REST DockLite.`
  - `wsl-docker-service/internal/httpserver/register.go`: `// Package httpserver đăng ký toàn bộ route HTTP cho docklite-wsl.`
  - `wsl-docker-service/internal/dockerengine/client.go`: `// Package dockerengine cung cấp Docker Engine API client ...`
- Import blank để giữ package chỉ vì side-effect được chú giải tiếng Việt: `_ "docklite-wsl/internal/settings" // gói dành cho cấu hình server sau này` (`wsl-docker-service/cmd/server/main.go`).

**Tên:**
- Hàm/type/const xuất: `PascalCase` (`WriteSuccess`, `WriteError`, `RequireBearerToken`, `LogRequests`, `ReadHeaderTimeout`).
- Hàm nội bộ: `camelCase` (`listContainers`, `runDockerAction`, `formatContainerPorts`, `matchBearerOrHeader`, `normalizeTrivyFormat`, `requestID`).
- Type không xuất thường dùng cho body/JSON: `trivyScanBody`, `healthResponse`, `envelope`.
- Const nhóm trong `const ( ... )` dùng chung tiền tố mô tả: `CodeValidation`, `CodeNotFound`, `CodeDockerCli`, `CodeDockerUnavailable` (`wsl-docker-service/internal/apiresponse/apiresponse.go`). Các giá trị chuỗi đúng bằng mã phía C# `DockLite.Contracts.Api.DockLiteErrorCodes` để hai đầu không lệch.

## Cấu trúc file C#

**Using:** đặt ngoài `namespace`, gom thành một khối đầu file, không sắp xếp tự động bởi analyzer (không có `.editorconfig`/analyzer bắt buộc). Mẫu quan sát được (trong `src/DockLite.App/AppStartupCoordinator.cs`):

```csharp
using DockLite.App.Services;
using DockLite.Core.Diagnostics;
using DockLite.Infrastructure.Wsl;

namespace DockLite.App;
```

Thứ tự thực tế: `System.*` trước, sau đó các thư viện bên ngoài (`CommunityToolkit.Mvvm.*`, `Microsoft.Extensions.*`), cuối cùng là các namespace `DockLite.*`.

**Namespace:** luôn file-scoped (`namespace X;`), không dùng block namespace.

**XML doc:** bắt buộc cho public API có ý nghĩa (interface, property public, static helper). Nội dung bằng tiếng Việt — ví dụ trong `src/DockLite.Core/Configuration/AppSettings.cs` mỗi property có `/// <summary>...</summary>` mô tả ngữ nghĩa nghiệp vụ. Kế thừa tài liệu khi triển khai interface bằng `/// <inheritdoc />` (xem `AppStartupCoordinator.RunInitialLoadAsync`).

**Khởi tạo:** `readonly` cho dependency inject vào constructor; dùng collection initializer/`new()` khi kiểu đã rõ:

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNameCaseInsensitive = true,
};
```

## MVVM (WPF)

- Dùng `CommunityToolkit.Mvvm` 8.4.0 (`src/DockLite.App/DockLite.App.csproj`).
- ViewModel là `public partial class ... : ObservableObject` để cho phép generator phát sinh property từ `[ObservableProperty]` trên field tiền tố `_`:

```csharp
[ObservableProperty]
private string _commandOutput = string.Empty;

[ObservableProperty]
private bool _isBusy;
```

Xem `src/DockLite.App/ViewModels/CleanupViewModel.cs`.

- Hàm lệnh async dùng `[RelayCommand]` (generator) — không tự dựng `ICommand`. Gọi từ UI qua `RefreshCommand.ExecuteAsync(null)` (xem `src/DockLite.App/AppStartupCoordinator.cs`).
- ViewModel lớn có thể tách partial theo nhóm tính năng (ví dụ `ContainersViewModel.BatchStats.cs`, `SettingsViewModel.WslCommands.cs`).

## Dependency Injection

- Đăng ký qua extension method đặt tại `src/DockLite.App/ServiceCollectionExtensions.cs` (`AddDockLiteUi`). App entrypoint trong `src/DockLite.App/App.xaml.cs` build `ServiceProvider`, lấy shell, `MainWindow`, và dispose trong `OnExit`.
- Lifetimes: hầu hết `AddSingleton<>` cho service UI (dialog, notification, shell, startup coordinator, api client, view model). Chưa thấy sử dụng `Scoped`/`Transient` trong ứng dụng desktop.
- Thao tác lấy dependency bên trong factory dùng `sp.GetRequiredService<T>()`, không dùng `GetService<T>()` + null-check.
- Interface đặt cạnh triển khai trong `src/DockLite.App/Services/` theo cặp `I<Name>.cs` + `<Name>.cs` (ví dụ `IDialogService.cs` + `WpfDialogService.cs`, `INotificationService.cs` + `WpfToastNotificationService.cs`).

## Async patterns

- Signature chuẩn: `public async Task<T> DoAsync(..., CancellationToken cancellationToken = default)`. Tham số `CancellationToken` luôn mang tên đầy đủ `cancellationToken`, để cuối danh sách, có giá trị mặc định.
- Hạ tầng (`DockLite.Infrastructure`, `DockLite.Core`) gọi `.ConfigureAwait(false)` cho mọi `await` — ví dụ `src/DockLite.Infrastructure/Api/DockLiteApiClient.cs`:

```csharp
using var response = await _session.Client.GetAsync("api/health", cancellationToken).ConfigureAwait(false);
response.EnsureSuccessStatusCode();
return await response.Content
    .ReadFromJsonAsync<HealthResponse>(JsonOptions, cancellationToken)
    .ConfigureAwait(false);
```

- Coordinator UI/app shell gọi `.ConfigureAwait(true)` khi cần về lại dispatcher sau `await` (xem `src/DockLite.App/AppStartupCoordinator.cs`).
- Kết hợp token của caller và app shutdown bằng `CancellationTokenSource.CreateLinkedTokenSource(...)`. Không quên `using` cho linked CTS.
- `HttpResponseMessage` đặt trong `using var response = await ...` để dispose đúng lúc.
- Retry cấp hạ tầng dùng helper chung `HttpReadRetry.ExecuteAsync(...)` / `ExecuteNullableAsync(...)` (`src/DockLite.Infrastructure/Api/HttpReadRetry.cs`). Không lặp try/catch thủ công trong từng endpoint.

## Xử lý lỗi C#

- Biên ứng dụng WPF bắt mọi exception chưa xử lý tại 3 điểm trong `src/DockLite.App/App.xaml.cs`:
  - `DispatcherUnhandledException` → `e.Handled = true`, log file, hiển thị `MessageBox` với thông điệp đã localize.
  - `AppDomain.CurrentDomain.UnhandledException` → log file, nuốt lỗi ghi log (nhận xét `// bỏ qua: lỗi ghi log không được làm hỏng quy trình tắt.`).
  - `TaskScheduler.UnobservedTaskException` → log + `SetObserved()`.
- Thông điệp cho người dùng sinh qua `NetworkErrorMessageMapper.FormatForUser(ex)` ở `src/DockLite.App/Services/NetworkErrorMessageMapper.cs` — ánh xạ `HttpRequestException`, `SocketException`, `TaskCanceledException`, `AggregateException`, `WebException`, `AuthenticationException` sang chuỗi đã localize, có `MaxDepth = 5` để tránh đệ quy vô hạn trên `AggregateException` lồng nhau.
- Lỗi tầng hạ tầng không ném ra UI dưới dạng exception mạng: API client trả `ApiResult<T>` (`src/DockLite.Contracts/Api/ApiResult.cs` + `ApiEnvelopeExtensions.cs`). Caller kiểm tra `result.Success` / `result.Error?.Code`.
- Catch `OperationCanceledException` riêng cho dòng `await` có thể bị shutdown/huỷ (xem `AppStartupCoordinator.RunInitialLoadAsync`), không log như lỗi.
- `catch { }` trần chỉ dùng trong 2 ngữ cảnh: ghi log (không được làm hỏng flow) và các đường dọn dẹp khi app tắt. Luôn có comment tiếng Việt giải thích (ví dụ `// bỏ qua: lỗi ghi log không được làm hỏng quy trình tắt.`).

## Xử lý lỗi Go

- Check-và-return sớm, không bọc `if err == nil` lồng. Mỗi handler HTTP lặp cụm:

```go
dc, err := dockerengine.Client()
if err != nil {
    apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
    return
}
```

Xem `wsl-docker-service/internal/docker/containers.go` và `wsl-docker-service/internal/docker/health.go`.

- Lỗi từ Docker Engine API được quy ra mã domain chung qua helper `dockerengine.WriteError(w, err)` trong `wsl-docker-service/internal/dockerengine/apierr.go`:
  - `errdefs.IsNotFound(err)` → `CodeNotFound` / 404.
  - `errdefs.IsConflict(err)` → `CodeConflict` / 409.
  - mặc định → `CodeDockerCli` / 500.
- Lỗi validate input dùng `apiresponse.CodeValidation` + 400, kèm message tiếng Việt: `apiresponse.WriteError(w, apiresponse.CodeValidation, "thiếu imageRef", http.StatusBadRequest)` (`wsl-docker-service/internal/docker/image_trivy_scan.go`).
- Lỗi chi tiết dài (stdout/stderr lệnh compose/prune) đi qua `apiresponse.WriteErrorWithDetails(...)` để đẩy phần `details` cho phía UI hiển thị.
- Panic không được dùng trong flow thường; `log.Fatal` chỉ ở `cmd/server/main.go` khi `ListenAndServe` trả lỗi khác `http.ErrServerClosed`.

## Envelope API dùng chung .NET/Go

Đường biên REST giữa hai nhánh được khoá bằng envelope JSON thống nhất:

- Phía Go: type `envelope { Success, Data, Error }` trong `wsl-docker-service/internal/apiresponse/apiresponse.go`, xuất qua `WriteSuccess` / `WriteError` / `WriteErrorWithDetails`.
- Phía .NET: `ApiEnvelope<T>` trong `src/DockLite.Contracts/Api/ApiEnvelope.cs`, với mapper `ToApiResult()` trong `ApiEnvelopeExtensions.cs`.
- Mã lỗi domain là nguồn sự thật duy nhất giữa hai bên: hằng số Go `CodeXxx` (`apiresponse.go`) phải khớp chính xác chuỗi trong `src/DockLite.Contracts/Api/DockLiteErrorCodes.cs` (`VALIDATION`, `NOT_FOUND`, `CONFLICT`, `DOCKER_CLI`, `DOCKER_UNAVAILABLE`, `INTERNAL`, `BAD_GATEWAY`, `COMPOSE_COMMAND`). Khi thêm mã mới phải sửa cả hai file và có test contract trong `tests/DockLite.Tests/ApiEnvelopeJsonTests.cs` / `ApiResultEnvelopeTests.cs`.
- JSON property dùng camelCase; client C# bật `PropertyNameCaseInsensitive = true` khi deserialize để chịu được khác biệt casing.

## Logging

**C#:**
- Log ứng dụng ghi file qua `DockLite.Core.Diagnostics.AppFileLog` (`src/DockLite.Core/Diagnostics/AppFileLog.cs`): `%LocalAppData%\DockLite\logs\docklite-yyyyMMdd.log`, mỗi dòng `ISO-8601 UTC\tcategory\tmessage`, lock `Sync` tĩnh để tuần tự hoá ghi.
- Exception ghi đầy đủ bằng `AppFileLog.WriteException(category, ex)` — giữ nhiều dòng stack trong file.
- Telemetry cục bộ tuỳ chọn qua `DockLite.Core.Diagnostics.DiagnosticTelemetry` (bật bằng `AppSettings.DiagnosticLocalTelemetryEnabled`). Không gửi mạng, không được log mật khẩu/body API — quy tắc này lặp lại trong docstring `AppSettings.DiagnosticLocalTelemetryEnabled`.
- Không dùng `ILogger<T>` / `Microsoft.Extensions.Logging` trong codebase hiện tại — toàn bộ qua static helper.

**Go:**
- Dùng `log/slog` với format key/value: `slog.Info("http_request", "method", r.Method, "path", r.URL.Path, "req_id", id)` (`wsl-docker-service/internal/httpserver/logging.go`).
- Tên event theo snake_case: `http_request`, `http_request_done`, `docklite-wsl_listen`.
- Middleware `LogRequests` gắn `X-Request-ID` (chuẩn hoá nếu client gửi, tự sinh 8 hex nếu không) và đặt vào `context.Context` qua khoá `ctxKeyRequestID{}`; ghi thêm `ms` khi kết thúc.
- `log.Fatal` chỉ dùng trong `cmd/server/main.go`. Không dùng `fmt.Println` cho log runtime.

## Bình luận

- Ngôn ngữ: tiếng Việt cho cả XML doc C# và doc comment Go, khớp user rule repo. Ví dụ `src/DockLite.Core/Configuration/AppSettings.cs` tất cả `<summary>` đều bằng tiếng Việt.
- Giải thích “tại sao”, đặc biệt các mẹo tránh bug cụ thể — ví dụ trong `src/DockLite.Infrastructure/Wsl/WslPathNormalizer.cs` có bình luận dài giải thích lý do không dùng `Path.GetFullPath` với UNC WSL và tại sao đổi `\` thành `/`.
- Bình luận khối `catch { }` trần bắt buộc có giải thích (“bỏ qua”, “lỗi ghi log không được làm hỏng quy trình tắt.”).
- Không được thêm icon/emoji vào code, tài liệu hay markdown (user rule của repo).

## Thiết kế hàm / module

**C#:**
- Constructor nhận phụ thuộc theo parameter list, mỗi line một dependency khi dài (xem `AppStartupCoordinator`). Không dùng property injection.
- Return early guard clause: kiểm tra `string.IsNullOrWhiteSpace`, `depth > MaxDepth` ở đầu hàm (xem `WslPathNormalizer`, `NetworkErrorMessageMapper`).
- Out parameter dùng cho parser có thể fail không ném exception: `bool TryParseVersionLine(string, out Version?, out string? err)` (`src/DockLite.Core/DockLiteSourceVersion.cs`), `bool TryUnixPathFromWslUnc(..., out string unixPath, out string? mismatchHint)` (`WslPathNormalizer.cs`).
- `string.Empty` thay vì `""` cho khởi tạo property/default.

**Go:**
- Hàm handler nhận `(w http.ResponseWriter, r *http.Request)`; khi cần thêm id phía sau: `containerLogs(w, r, id)` (`wsl-docker-service/internal/docker/containers.go`).
- Luôn lấy `ctx := r.Context()` thay vì `context.Background()` trong handler, để middleware `RequestContextTimeout` (`wsl-docker-service/internal/httpserver/context_timeout.go`) kiểm soát được thời gian.
- `json.NewDecoder(r.Body).Decode(&body)` cho body vào; `json.NewEncoder(w).Encode(env)` cho body ra. Không buffer vào byte slice trừ khi cần biến đổi tiếp.
- Singleton dùng `sync.Once` + package-level `var` (`wsl-docker-service/internal/dockerengine/client.go`).

## Cấu hình & hằng

- C#: hằng cho đường dẫn/URL/timeout nằm trong `DockLite.Core.Configuration` (`AppSettings`, `AppSettingsDefaults`, `DockLiteDefaults`, `ServiceBaseUriHelper`). Mọi giá trị người dùng chỉnh được đều có docstring mô tả range/default (ví dụ `WslAutoStartHealthWaitSeconds (10-600, default 30)`).
- Go: hằng timeout xuất từ package `httpserver` để `main` dùng lại: `ReadHeaderTimeout`, `ReadTimeout`, `WriteTimeout`, `IdleTimeout` (`wsl-docker-service/internal/httpserver/register.go` + `limits.go`).
- Đường dẫn/địa chỉ mặc định để trong hằng tại điểm dùng, không rải magic string — ví dụ `addr := "0.0.0.0:17890"` trong `cmd/server/main.go` kèm bình luận giải thích lý do bind 0.0.0.0 (forward WSL2 ổn định hơn 127.0.0.1).
- Biến môi trường được đọc ở biên `main`: `DOCKLITE_ADDR`, `DOCKLITE_API_TOKEN`. Không có package đọc env tập trung — giữ local ở `main.go`.

## Khớp .NET ↔ Go (đường biên)

Khi chỉnh sửa một phía, luôn kiểm tra và cập nhật phía còn lại theo thứ tự:

1. Sửa hằng mã lỗi hoặc schema JSON → sửa đồng thời `wsl-docker-service/internal/apiresponse/apiresponse.go` và `src/DockLite.Contracts/Api/DockLiteErrorCodes.cs` (hoặc DTO tương ứng trong `src/DockLite.Contracts/Api/`).
2. Sửa route → cập nhật `wsl-docker-service/internal/httpserver/register.go` và `wsl-docker-service/internal/httpserver/openapi.json` (nhúng vào binary qua `OpenAPI` handler) + method trên `src/DockLite.Infrastructure/Api/DockLiteApiClient.cs`.
3. Sửa envelope error mapping → đảm bảo `ApiEnvelopeExtensions.ToApiResult()` vẫn trả đúng `DockLiteErrorCodes.*` khi body rỗng (`UNKNOWN`) hoặc JSON hỏng (`PARSE`).
4. Thêm test contract trong `tests/DockLite.Tests/HealthResponseContractTests.cs` hoặc file `*EnvelopeTests.cs` để khoá schema.

---

*Convention analysis: 2026-04-23*
