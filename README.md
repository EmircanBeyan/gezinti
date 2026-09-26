# Gezinti

Farklı şehir ve bölgelerde kişiselleştirilmiş mekân, etkinlik ve gezi rotaları keşfetmeyi sağlayan bir platform.

## Yerelde çalıştırma

İlk demo akışı harita üzerinde çevredeki mekânları arar. Backend için .NET 10 SDK ve Docker, frontend için Node.js gerekir.

1. Proje kökünde PostGIS veritabanını başlat:

   ```sh
   docker compose up -d
   ```

2. Backend bağlantı bilgisini ayarla, veritabanı migration'larını uygula ve API'yi başlat:

   ```sh
   export ConnectionStrings__DefaultConnection='Host=localhost;Port=5433;Database=gezinti;Username=postgres;Password=gezinti_dev_password'
   dotnet ef database update --project backend/Gezinti.Infrastructure --startup-project backend/Gezinti.API
   dotnet run --project backend/Gezinti.API --launch-profile http
   ```

   API `http://localhost:5056` adresinde açılır.

3. Başka bir terminalde frontend'i başlat:

   ```sh
   cd frontend
   npm install
   npm run dev
   ```

   Uygulamayı Vite'ın gösterdiği adreste aç (varsayılan `http://localhost:5173`). Harita döşemeleri OpenStreetMap'ten, mekân verileri Overpass API'sinden gelir.
