# Gezinti

Gezinti, kullanıcının seçtiği konumun çevresindeki yerleri keşfetmesine yardımcı olan konum tabanlı bir web uygulamasıdır. Şehir, ilçe, mahalle veya sokak aramasıyla haritada bir konuma gidilebilir; ardından yakındaki mekânlar harita üzerinde incelenebilir.

> Uygulamanın kaynak kodu şu anda `develop` branch’indedir. `main` branch’i proje tanıtımı ve başlangıç yönergelerini içerir.

## Özellikler

- Şehir, ilçe, mahalle ve sokak adına göre konum arama
- Seçilen konum çevresindeki mekânları kategoriye ve arama yarıçapına göre keşfetme
- Harita üzerinde konumları ve sonuçları görüntüleme
- Yakındaki mekân verilerini OpenStreetMap servislerinden alma ve PostGIS ile saklama

## Teknolojiler

- **Frontend:** React, TypeScript, Vite, Leaflet
- **Backend:** .NET 10, ASP.NET Core Web API, Entity Framework Core
- **Veritabanı:** PostgreSQL ve PostGIS
- **Harita ve coğrafi veriler:** OpenStreetMap, Nominatim ve Overpass API

## Gereksinimler

- Git
- Docker Desktop ve Docker Compose
- .NET 10 SDK
- Node.js ve npm

## Projeyi çalıştırma

### 1. Kaynak kodunu alın

```bash
git clone https://github.com/EmircanBeyan/gezinti.git
cd gezinti
git switch develop
```

### 2. Veritabanını başlatın

```bash
docker compose up -d postgres
```

PostGIS veritabanı varsayılan olarak `localhost:5433` adresinde çalışır. Yerel geliştirme bağlantı ayarları `backend/Gezinti.API/appsettings.json` dosyasındadır.

Docker Compose geliştirme veritabanı için `gezinti_dev_password` parolasını kullanır. API ve EF Core komutlarının aynı bağlantıyı kullanması için terminalde bağlantı değişkenini ayarlayın.

macOS/Linux (bash veya zsh):

```bash
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5433;Database=gezinti;Username=postgres;Password=gezinti_dev_password'
```

Windows PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5433;Database=gezinti;Username=postgres;Password=gezinti_dev_password"
```

### 3. Veritabanı şemasını oluşturun

EF Core komut satırı aracı kurulu değilse bir kez yükleyin:

```bash
dotnet tool install --global dotnet-ef
```

Ardından proje kök dizininde migration’ları uygulayın:

```bash
dotnet ef database update \
  --project backend/Gezinti.Infrastructure \
  --startup-project backend/Gezinti.API
```

### 4. API’yi başlatın

```bash
dotnet run --project backend/Gezinti.API --launch-profile http
```

API `http://localhost:5056` adresinde, Swagger arayüzü ise `http://localhost:5056/swagger` adresinde açılır.

### 5. Frontend’i başlatın

Yeni bir terminal açıp proje kök dizininden çalıştırın:

```bash
cd frontend
npm install
npm run dev
```

Frontend varsayılan olarak `http://localhost:5173` adresinde açılır. API adresini değiştirmek gerekirse `frontend/.env` dosyasına aşağıdaki satırı ekleyin:

```env
VITE_API_BASE_URL=http://localhost:5056
```

## Dış servisler ve veri kullanımı

Konum araması ve yakındaki OpenStreetMap verileri için internet bağlantısı gerekir. OpenStreetMap verileri lisans ve atıf koşullarına tabidir; ayrıntılar için [OpenStreetMap Copyright](https://www.openstreetmap.org/copyright) sayfasına bakın. Nominatim ve Overpass gibi herkese açık servisleri kullanırken ilgili servislerin kullanım politikalarına uyun.

## Durdurma

API ve frontend terminallerinde `Ctrl+C` tuşlarına basın. Veritabanı container’ını durdurmak için proje kök dizininde:

```bash
docker compose down
```

Veritabanı verilerini de silmek isterseniz `docker compose down -v` kullanabilirsiniz; bu işlem yerel veritabanı volume’unu kaldırır.
