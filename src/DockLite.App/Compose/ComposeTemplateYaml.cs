namespace DockLite.App.Compose;

/// <summary>
/// Nội dung file compose mẫu theo từng stack (chỉ gợi ý khởi đầu; cần chỉnh biến môi trường và volume).
/// </summary>
public static class ComposeTemplateYaml
{
    public const string IdNginx = "nginx";
    public const string IdLaravel = "laravel";
    public const string IdNode = "node";
    public const string IdJava = "java";

    /// <summary>
    /// Trả về YAML hoặc null nếu không có id.
    /// </summary>
    public static string? TryGet(string templateId)
    {
        return templateId switch
        {
            IdNginx => NginxMinimal,
            IdLaravel => LaravelStack,
            IdNode => NodeStack,
            IdJava => JavaStack,
            _ => null,
        };
    }

    /// <summary>
    /// Nginx tối thiểu.
    /// </summary>
    private const string NginxMinimal = """
services:
  web:
    image: nginx:alpine
    ports:
      - "8080:80"
""";

    /// <summary>
    /// Gợi ý PHP/Laravel: Apache + PHP extension + MySQL (đặt mã trong thư mục hiện tại; chỉnh .env).
    /// </summary>
    private const string LaravelStack = """
services:
  web:
    image: php:8.3-apache
    ports:
      - "8080:80"
    volumes:
      - ./:/var/www/html
    environment:
      APACHE_DOCUMENT_ROOT: /var/www/html/public
    depends_on:
      - mysql

  mysql:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: changeme
      MYSQL_DATABASE: laravel
    ports:
      - "3306:3306"
    volumes:
      - mysql_data:/var/lib/mysql

volumes:
  mysql_data:
""";

    /// <summary>
    /// Gợi ý Node: ứng dụng npm trong thư mục hiện tại (chỉnh script trong package.json).
    /// </summary>
    private const string NodeStack = """
services:
  app:
    image: node:20-alpine
    working_dir: /app
    volumes:
      - ./:/app
    ports:
      - "3000:3000"
    command: sh -c "npm ci && npm run start"
    environment:
      NODE_ENV: development
""";

    /// <summary>
    /// Gợi ý Java/JVM: chạy jar (đặt app.jar hoặc sửa lệnh).
    /// </summary>
    private const string JavaStack = """
services:
  app:
    image: eclipse-temurin:21-jre-alpine
    working_dir: /app
    volumes:
      - ./:/app
    ports:
      - "8080:8080"
    command: ["java", "-jar", "/app/app.jar"]
    environment:
      JAVA_OPTS: "-Xms256m -Xmx512m"
""";
}
