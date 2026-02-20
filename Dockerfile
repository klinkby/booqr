FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine3.22-aot AS build
WORKDIR /source

# Build the app
COPY . .
RUN --mount=type=cache,target=/root/.nuget \
    --mount=type=cache,target=/source/bin \
    --mount=type=cache,target=/source/obj \
    dotnet publish -o /app src/Klinkby.Booqr.Api/Klinkby.Booqr.Api.csproj \
        -r linux-musl-x64 \
        -p:DebugType=None

# Final stage/image
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine3.22
WORKDIR /app
COPY --from=build /app .
ENV DOTNET_SYSTEM_NET_SOCKETS_IO_URING=1
USER $APP_UID
ENTRYPOINT ["/app/Klinkby.Booqr.Api"]
