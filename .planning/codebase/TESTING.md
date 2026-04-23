# Testing Patterns

**Analysis Date:** 2026-04-23

DockLite có hai bộ test độc lập chạy ở hai môi trường khác nhau:

- Test .NET (xUnit) chạy trên Windows: `tests/DockLite.Tests/`.
- Test Go (`testing` chuẩn) chạy trong WSL: `wsl-docker-service/internal/**/*_test.go` và `wsl-docker-service/integration/`.

Quy trình tối thiểu trước khi release (theo `README.md`): chạy đủ cả hai.

## Framework & công cụ

**.NET:**
- Runner: xUnit `2.9.3` (`tests/DockLite.Tests/DockLite.Tests.csproj`).
- Visual Studio / `dotnet test` runner: `xunit.runner.visualstudio 3.1.4`, `Microsoft.NET.Test.Sdk 17.14.1`.
- Coverage: `coverlet.collector 6.0.4` (sẵn sàng cho `dotnet test --collect:"XPlat Code Coverage"`; không có cấu hình enforce).
- Target: `net8.0-windows` với `<UseWPF>false</UseWPF>` — đủ để reference cả `DockLite.App` (WPF) nhưng không kéo XAML runtime.
- Global using cho test: `<Using Include="Xunit" />` trong csproj, nghĩa là test file không cần `using Xunit;` ở đầu (nhưng mẫu hiện tại vẫn có thể thêm).
- Assertion: `Xunit.Assert` (ví dụ `Assert.True`, `Assert.Equal`, `Assert.Contains`, `Assert.Empty`, `Assert.Single`).
- Không dùng mocking framework (không thấy `Moq`, `NSubstitute`, `FakeItEasy` trong package references). Test dạng pure unit với static helper, parser, DTO JSON; không có abstraction bị mock.

**Go:**
- Runner: package `testing` chuẩn của Go, chạy qua `go test`.
- HTTP test: `net/http/httptest` (xem `wsl-docker-service/internal/httpserver/openapi_test.go`, `wsl-docker-service/integration/api_integration_test.go`).
- Không dùng thư viện assertion ngoài (không `testify`, không `gotest.tools` chủ động — `gotest.tools/v3` chỉ xuất hiện như indirect dep từ Docker SDK).
- Tách test tích hợp bằng build tag `//go:build integration` (dòng đầu tệp), ví dụ `wsl-docker-service/integration/api_integration_test.go`.

## Lệnh chạy test

**.NET (PowerShell, ở root repo):**
```powershell
dotnet test DockLite.slnx -c Release
```

Chạy một file hoặc một class:
```powershell
dotnet test tests/DockLite.Tests/DockLite.Tests.csproj --filter "FullyQualifiedName~WslPathNormalizerTests"
```

Coverage (không bắt buộc, không có ngưỡng enforce):
```powershell
dotnet test DockLite.slnx -c Release --collect:"XPlat Code Coverage"
```

**Go (bash trong WSL, ở `wsl-docker-service/`):**
```bash
bash scripts/test-go.sh          # go vet ./... + go test ./... (đơn vị)
bash scripts/test-integration.sh # go test -tags=integration -count=1 -v ./integration/...
```

Nội dung hai script (`wsl-docker-service/scripts/test-go.sh`, `wsl-docker-service/scripts/test-integration.sh`): đều có `set -eu` và `set -o pipefail`, tự `cd` về root module; `test-integration.sh` thêm kiểm tra `docker info` trước, thoát 1 nếu daemon không chạy.

**Gộp một dòng (theo README):**
```powershell
dotnet test DockLite.slnx -c Release; wsl -e bash -lc "cd ~/your/path/wsl-docker-service && bash scripts/test-go.sh"
```

## Tổ chức file test

**.NET:** dự án test tách riêng tại `tests/DockLite.Tests/`, không co-locate cạnh source. Một file test cho một class/helper được kiểm:

```
tests/DockLite.Tests/
  ApiEnvelopeJsonTests.cs
  ApiResultEnvelopeTests.cs
  AppSettingsDefaultsTests.cs
  ComposeComposePathsTests.cs
  DockLiteSourceVersionTests.cs
  HealthResponseContractTests.cs
  NetworkErrorMessageMapperTests.cs
  WslPathNormalizerTests.cs
```

Project tham chiếu cả bốn project trong `src/` (`DockLite.App`, `DockLite.Contracts`, `DockLite.Core`, `DockLite.Infrastructure`) nên test có thể truy cập bất kỳ public type nào.

**Go:** co-locate cạnh source trong cùng package (white-box, cùng `package httpserver`, `package wslimit`) để chạm field/function không xuất:

- `wsl-docker-service/internal/wslimit/wslimit_test.go` — unit test package `wslimit`.
- `wsl-docker-service/internal/httpserver/openapi_test.go` — unit test cùng `package httpserver`, truy cập biến nội bộ `openAPISpec` và hàm `OpenAPI`.
- `wsl-docker-service/integration/api_integration_test.go` — integration test, đặt trong package `integration_test` (external), dùng build tag `//go:build integration`.

## Đặt tên

**.NET:**
- File: `<TypeUnderTest>Tests.cs` (`WslPathNormalizerTests.cs`, `ApiResultEnvelopeTests.cs`). Luôn có hậu tố `Tests`.
- Class test: `public sealed class <Name>Tests`, cùng namespace `DockLite.Tests`.
- Method test:
  - Tiếng Anh kiểu `Action_condition_expected`: `ToApiResult_success_ok`, `Deserialize_success_envelope_with_data`, `Normalize_clamp_http_timeout_seconds`.
  - Hỗn hợp Việt không dấu khi mô tả business bằng tiếng Việt chuẩn hơn, vẫn snake_case: `ParseComposeFileLines_null_va_rong_tra_ve_rong`, `NormalizeForWslpathArgument_drive_path_dung_dau_xuoi_tranh_loi_wslpath`, `SocketException_tra_ve_goi_y_ket_noi`.
- Không có convention fixture/collection attribute — test class độc lập, không chia sẻ state qua `IClassFixture`.

**Go:**
- File: `<subject>_test.go` cùng thư mục (`wslimit_test.go`, `openapi_test.go`).
- Hàm test: `TestSubject_condition` hoặc mô tả hành vi — `TestTryAcquireRelease`, `TestOpenAPISpec_embeddedJSONValid`, `TestOpenAPI_handlerGET`, `TestOpenAPI_handlerMethodNotAllowed`, `TestAPIHealth`, `TestAPIDockerInfoWithEngine`.

## Cấu trúc test (.NET)

**Đơn lẻ (`[Fact]`):**

```csharp
[Fact]
public void ToApiResult_success_ok()
{
    var env = new ApiEnvelope<DockerInfoData>
    {
        Success = true,
        Data = new DockerInfoData { ServerVersion = "24.0", /* ... */ },
    };
    ApiResult<DockerInfoData> r = env.ToApiResult();
    Assert.True(r.Success);
    Assert.NotNull(r.Data);
    Assert.Equal("24.0", r.Data.ServerVersion);
}
```

Xem `tests/DockLite.Tests/ApiResultEnvelopeTests.cs`.

**Data-driven (`[Theory]` + `[InlineData]`):** được dùng nhiều cho normalization/clamp value — ví dụ `tests/DockLite.Tests/AppSettingsDefaultsTests.cs`:

```csharp
[Theory]
[InlineData("en", "en")]
[InlineData("EN", "en")]
[InlineData("", "vi")]
[InlineData("fr", "vi")]
public void Normalize_maps_ui_language_to_vi_or_en(string input, string expected)
{
    var s = new AppSettings { UiLanguage = input };
    AppSettingsDefaults.Normalize(s);
    Assert.Equal(expected, s.UiLanguage);
}
```

**Contract test JSON:** deserialize raw string và kiểm envelope — `tests/DockLite.Tests/HealthResponseContractTests.cs`, `tests/DockLite.Tests/ApiEnvelopeJsonTests.cs`. Dùng `System.Text.Json.JsonSerializer` + `JsonSerializerOptions { PropertyNameCaseInsensitive = true }`. Các test này khoá định dạng wire giữa service Go và client C#.

**Skip theo OS:** `WslPathNormalizerTests` tự bỏ qua khi không phải Windows:

```csharp
if (!OperatingSystem.IsWindows())
{
    return;
}
```

Không dùng `[Fact(Skip = "...")]` cho mục đích này.

**Không dùng:** `[ClassData]`, `[MemberData]`, `ITestOutputHelper`, `IAsyncLifetime`, collection fixture, snapshot testing. Không có base class chung cho test.

## Cấu trúc test (Go)

**Unit (white-box, cùng package):**

```go
package wslimit

import "testing"

func TestTryAcquireRelease(t *testing.T) {
    if !TryAcquireWebSocket() {
        t.Fatal("first acquire must succeed")
    }
    ReleaseWebSocket()
}
```

Xem `wsl-docker-service/internal/wslimit/wslimit_test.go`.

**HTTP handler + httptest (unit):**

```go
func TestOpenAPI_handlerGET(t *testing.T) {
    req := httptest.NewRequest(http.MethodGet, "/api/openapi.json", nil)
    rec := httptest.NewRecorder()
    OpenAPI(rec, req)
    if rec.Code != http.StatusOK {
        t.Fatalf("status=%d", rec.Code)
    }
    ct := rec.Header().Get("Content-Type")
    if !strings.HasPrefix(ct, "application/json") {
        t.Fatalf("Content-Type=%q", ct)
    }
}
```

Xem `wsl-docker-service/internal/httpserver/openapi_test.go`.

**Integration (build tag + server thật):**

```go
//go:build integration

package integration_test

func TestAPIHealth(t *testing.T) {
    mux := http.NewServeMux()
    httpserver.Register(mux)
    srv := httptest.NewServer(mux)
    defer srv.Close()

    resp, err := http.Get(srv.URL + "/api/health")
    if err != nil { t.Fatal(err) }
    defer resp.Body.Close()
    // ...
}
```

Xem `wsl-docker-service/integration/api_integration_test.go`. Test `TestAPIDockerInfoWithEngine` dùng `t.Skip(...)` khi Docker Engine chưa sẵn sàng thay vì `t.Fatal`, để pipeline không fail trên máy không có Docker.

**Phong cách fail:** dùng `t.Fatal` / `t.Fatalf` ngay khi assert sai (không có assertion library). Thông điệp fail hỗn hợp tiếng Anh và tiếng Việt (`"thiếu paths"`, `"envelope không thành công: %s"`, `"first acquire must succeed"`).

## Mocking & fakes

- **.NET:** không có mocking framework. Toàn bộ test hiện tại nhắm vào code thuần (parser, normalizer, mapper lỗi, deserialize envelope, clamp setting). Nếu cần test dịch vụ phụ thuộc HTTP/disk, pattern được khuyến nghị theo codebase: viết interface trong `DockLite.Core.Services` (tương tự `IDockLiteApiClient`) rồi tạo fake bằng class test-local — không thêm Moq/NSubstitute.
- **Go:** không có mocking framework. Test mạng dùng `httptest.NewServer` với `mux` thật từ `httpserver.Register` (integration) hoặc gọi trực tiếp handler với `httptest.NewRecorder` (unit). Docker client thật được dùng; nếu không có, test `t.Skip(...)`.

## Fixtures & test data

- **.NET:** không có thư mục `fixtures/` hoặc factory helper. Dữ liệu test inline trong từng method — string JSON dùng raw literal `"""{...}"""` (C# 11) cho tính đọc được. Xem ví dụ trong `tests/DockLite.Tests/ApiEnvelopeJsonTests.cs`:

```csharp
const string json = """{"success":true,"data":{"items":[]}}""";
```

- **Go:** không có fixture file, dữ liệu dựng inline trong `*_test.go`. OpenAPI spec test đọc biến package nội bộ `openAPISpec` (được embed) thay vì file trên disk.

## Phân lớp test (unit vs integration)

- **.NET:** mọi test hiện tại là unit. Không có test tích hợp gọi HTTP thật tới service Go. Kiểm contract giữa hai phía được khoá gián tiếp qua deserialize JSON literal trong `HealthResponseContractTests` và `ApiEnvelopeJsonTests` — khi chỉnh schema phía Go phải cập nhật các test này.
- **Go:**
  - Unit (`internal/**/*_test.go`): không yêu cầu Docker. Chạy bằng `bash scripts/test-go.sh`.
  - Integration (`integration/*_test.go`): cần Docker Engine (socket mặc định hoặc `DOCKER_HOST`) và build tag `integration`. Chạy bằng `bash scripts/test-integration.sh`. Script chặn sớm nếu `docker info` thất bại.

## Assertion patterns

**.NET:** chỉ dùng `Xunit.Assert`:

```csharp
Assert.True(r.Success);
Assert.False(env.Success);
Assert.Null(err);
Assert.NotNull(result);
Assert.Equal(expected, actual);
Assert.Equal(1.5, env.Data.Items[0].Stats!.CpuUsagePercent, 2);   // precision
Assert.Empty(ComposeComposePaths.ParseComposeFileLines(null));
Assert.Single(env.Data!.Items);
Assert.Contains("401", msg, StringComparison.Ordinal);
Assert.DoesNotContain('\\', n);
Assert.StartsWith("C:/", n, StringComparison.OrdinalIgnoreCase);
```

So sánh chuỗi có thể kèm `StringComparison.Ordinal` hoặc `StringComparison.OrdinalIgnoreCase` tuỳ ngữ cảnh.

**Go:** so sánh bằng toán tử Go thuần, fail qua `t.Fatalf`:

```go
if rec.Code != http.StatusOK { t.Fatalf("status=%d", rec.Code) }
if !strings.HasPrefix(ct, "application/json") { t.Fatalf("Content-Type=%q", ct) }
if success, ok := env["success"].(bool); !ok || !success { t.Fatalf(...) }
```

## Async / cancellation trong test

- **.NET:** các test hiện tại không có path async thật. `NetworkErrorMessageMapperTests.TaskCanceledException_khong_huy_la_timeout` dựng thủ công `TaskCanceledException` từ `CancellationTokenSource` và kiểm thông điệp — không `await` thật.
- **Go:** integration test dùng `context.Background()` cho `dc.Ping(ctx)` trong `TestAPIDockerInfoWithEngine` và dựa vào `defer resp.Body.Close()` + `defer srv.Close()` cho teardown.

## Khi thêm test mới

**.NET checklist:**
1. Thêm file `<Name>Tests.cs` vào `tests/DockLite.Tests/` (không co-locate).
2. Class `public sealed`, namespace `DockLite.Tests`.
3. Ưu tiên `[Theory]` + `[InlineData]` cho các hàm chuẩn hoá/clamp nhiều nhánh.
4. Khi đổi DTO hoặc mã lỗi, cập nhật test JSON literal trong `ApiEnvelopeJsonTests.cs` / `ApiResultEnvelopeTests.cs` / `HealthResponseContractTests.cs` để giữ schema sync với service Go.
5. Nếu test phụ thuộc Windows-only API (WSL path), thêm early return với `OperatingSystem.IsWindows()`.
6. Chạy `dotnet test DockLite.slnx -c Release` để xác nhận.

**Go checklist:**
1. Thêm file `<subject>_test.go` cùng thư mục với source; cùng package để chạm được symbol không xuất.
2. Hàm test `TestXxx(t *testing.T)`, fail bằng `t.Fatal`/`t.Fatalf`; `t.Skip` khi thiếu dependency (ví dụ không có Docker).
3. Với handler HTTP: dựng `httptest.NewRequest` + `httptest.NewRecorder` cho unit, `httptest.NewServer(mux)` cho integration.
4. Test integration phải có dòng đầu `//go:build integration` và nằm trong thư mục `wsl-docker-service/integration/`.
5. Chạy cục bộ: `bash scripts/test-go.sh`; integration: `bash scripts/test-integration.sh`.

---

*Testing analysis: 2026-04-23*
