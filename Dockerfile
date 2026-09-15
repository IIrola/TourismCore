# syntax=docker/dockerfile:1
#
# BIT (Turismo) — imagen de servicio y de migracion a la vez.
#
# El migrador viaja aca adentro a proposito: si el esquema viviera en su propio artefacto, nada
# garantizaria que el que corre sea el que corresponde al codigo que va a arrancar. Con una sola
# imagen comparten etiqueta y el problema desaparece.
#
#   docker run <imagen>                    -> la API
#   docker run <imagen> Tourism.Migrator.dll   -> aplica migraciones y termina
#
# Las dos formas leen ConnectionStrings__DefaultConnection del entorno, asi que se configuran
# igual. El migrador acepta ademas `--connection <valor>`, que gana sobre el entorno.
#
# Se construye desde la raiz del repo:  docker build -t TourismCore:dev .

# ---------------------------------------------------------------- restore
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Primero solo los csproj: mientras no cambien las referencias, el restore queda cacheado aunque
# cambie todo el codigo.
#
# Se restauran los dos proyectos que se publican y no la solucion, porque la solucion incluye los
# proyectos de prueba — que no tienen nada que hacer dentro de la imagen. Las pruebas las corre el
# pipeline.
COPY src/ ./probe/
RUN find ./probe -name '*.csproj' | while read -r f; do \
      rel="${f#./probe/}"; mkdir -p "./src/$(dirname "$rel")"; cp "$f" "./src/$rel"; \
    done \
 && rm -rf ./probe \
 && dotnet restore src/Tourism.Api/Tourism.Api.csproj \
 && dotnet restore src/Tourism.Migrator/Tourism.Migrator.csproj

# ---------------------------------------------------------------- build
FROM restore AS build
WORKDIR /src
COPY src/ ./src/

# Los dos al mismo directorio de publicacion: comparten runtime y bibliotecas, asi que la imagen
# no paga dos veces por ellas.
RUN dotnet publish src/Tourism.Api/Tourism.Api.csproj \
      -c Release -o /app/publish --no-restore /p:UseAppHost=false \
 && dotnet publish src/Tourism.Migrator/Tourism.Migrator.csproj \
      -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------------------------------------------------------------- runtime
# Chiseled: sin shell y sin gestor de paquetes, y corre como usuario no root por defecto. Menos
# superficie, y nada que ejecutar si alguien consigue colarse.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish ./

# 8080 porque un proceso no root no puede escuchar en 80. El TLS lo termina el proxy.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# El entrypoint es el runtime y el comando dice que programa: asi elegir el migrador es pasar otro
# argumento desde el compose, y no reescribir el entrypoint.
ENTRYPOINT ["dotnet"]
CMD ["Tourism.Api.dll"]
