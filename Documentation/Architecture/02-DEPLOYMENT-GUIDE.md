# Deployment and Setup Guide

## Prerequisites

### System Requirements

**Minimum**:
- OS: Windows 10/11, Linux, macOS
- RAM: 16 GB
- Storage: 50 GB free (for models)
- GPU: NVIDIA GPU with 8+ GB VRAM (for local generation)
- .NET: .NET 8.0 SDK

**Recommended**:
- RAM: 32 GB+
- Storage: 500 GB+ SSD
- GPU: NVIDIA RTX 3060+ with 12+ GB VRAM
- .NET: Latest .NET 8.0 SDK

### Required Software

1. **.NET 8.0 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Verify: `dotnet --version` should show 8.0.x

2. **Automatic1111 Stable Diffusion WebUI** (Primary)
   - Repository: https://github.com/AUTOMATIC1111/stable-diffusion-webui
   - Must be running on `http://localhost:7860`
   - API must be enabled: `--api` flag

3. **ComfyUI** (Optional)
   - Repository: https://github.com/comfyanonymous/ComfyUI
   - Must be running on `http://localhost:8188`
   - For advanced workflows and video generation

4. **Ollama** (Optional)
   - Download: https://ollama.ai/
   - For LLM-based prompt enhancement
   - Models: llama2, mistral, etc.

### Optional Tools

- **Git**: For cloning repository
- **Visual Studio 2022** or **VS Code**: For development
- **SQLite Browser**: For database inspection

---

## Installation Steps

### 1. Clone Repository

```bash
git clone https://github.com/Hugo-Matias/StableDiffusion-Blazor-WebApp.git
cd StableDiffusion-Blazor-WebApp/BlazorWebApp
```

### 2. Configure Application

Create `appsettings.json` in `BlazorWebApp/` directory:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=BlazorWebApp.db"
  },
  
  "OutputDir": "C:/StableDiffusion/outputs",
  "ResourcesPath": "C:/StableDiffusion/models",
  "ResourcePreviewsPath": "C:/StableDiffusion/previews",
  "ComfyUIPath": "C:/ComfyUI",
  
  "CivitaiApiToken": "your-civitai-api-token-here"
}
```

**Path Configuration**:

| Setting | Description | Example |
|---------|-------------|---------|
| OutputDir | Where generated images are saved | `C:/SD/outputs` |
| ResourcesPath | Where model files are stored | `C:/SD/models` |
| ResourcePreviewsPath | Preview images for models | `C:/SD/previews` |
| ComfyUIPath | ComfyUI installation directory | `C:/ComfyUI` |

**Notes**:
- Use forward slashes (`/`) even on Windows
- Paths must exist before starting app
- App will create subdirectories as needed

**CivitAI API Token**:
1. Create account at https://civitai.com
2. Go to Account Settings → API Keys
3. Generate new API key
4. Copy to `appsettings.json`

### 3. Create Required Directories

```bash
# Windows (PowerShell)
New-Item -ItemType Directory -Force -Path "C:/StableDiffusion/outputs"
New-Item -ItemType Directory -Force -Path "C:/StableDiffusion/models"
New-Item -ItemType Directory -Force -Path "C:/StableDiffusion/previews"

# Linux/macOS
mkdir -p ~/StableDiffusion/outputs
mkdir -p ~/StableDiffusion/models
mkdir -p ~/StableDiffusion/previews
```

### 4. Restore Dependencies

```bash
cd BlazorWebApp
dotnet restore
```

### 5. Build Application

```bash
dotnet build --configuration Release
```

### 6. Initialize Database

Database is created automatically on first run via Entity Framework migrations.

To manually apply migrations:
```bash
dotnet ef database update
```

---

## Running the Application

### Development Mode

```bash
dotnet run
```

Access at: `https://localhost:5001` or `http://localhost:5000`

### Production Mode

```bash
dotnet run --configuration Release
```

### Publish for Deployment

```bash
dotnet publish --configuration Release --output ./publish
```

Then run:
```bash
cd publish
dotnet BlazorWebApp.dll
```

---

## Backend Setup

### Automatic1111 WebUI Setup

1. **Install WebUI**:
   ```bash
   git clone https://github.com/AUTOMATIC1111/stable-diffusion-webui.git
   cd stable-diffusion-webui
   ```

2. **Configure WebUI**:
   
   Edit `webui-user.bat` (Windows) or `webui-user.sh` (Linux/Mac):
   ```bash
   set COMMANDLINE_ARGS=--api --listen --port 7860
   ```

3. **Start WebUI**:
   ```bash
   ./webui.bat          # Windows
   ./webui.sh           # Linux/Mac
   ```

4. **Verify**:
   - WebUI should be at `http://localhost:7860`
   - API docs at `http://localhost:7860/docs`

### ComfyUI Setup (Optional)

1. **Install ComfyUI**:
   ```bash
   git clone https://github.com/comfyanonymous/ComfyUI.git
   cd ComfyUI
   ```

2. **Install Dependencies**:
   ```bash
   pip install -r requirements.txt
   ```

3. **Start ComfyUI**:
   ```bash
   python main.py --listen --port 8188
   ```

4. **Verify**:
   - ComfyUI should be at `http://localhost:8188`

### Ollama Setup (Optional)

1. **Install Ollama**:
   - Download from https://ollama.ai/

2. **Pull Models**:
   ```bash
   ollama pull llama2
   ollama pull mistral
   ```

3. **Verify**:
   ```bash
   ollama list
   ```

---

## First-Time Setup

### 1. Start Required Services

In separate terminals:

```bash
# Terminal 1: WebUI
cd stable-diffusion-webui
./webui.bat --api

# Terminal 2: ComfyUI (optional)
cd ComfyUI
python main.py

# Terminal 3: Blazor App
cd StableDiffusion-Blazor-WebApp/BlazorWebApp
dotnet run
```

### 2. Access Application

Navigate to: `https://localhost:5001`

### 3. Create First Project

1. Click "Create Project" button
2. Enter project name (e.g., "Test Project")
3. Select or create folder

### 4. Download First Model

1. Go to Resources page
2. Click CivitAI tab
3. Search for a model (e.g., "Realistic Vision")
4. Click Download
5. Wait for completion

### 5. Generate First Image

1. Go to Txt2Img page (WebUI or ComfyUI)
2. Select downloaded model
3. Enter prompt (e.g., "a beautiful landscape")
4. Click Generate
5. View results in right panel

---

## Directory Structure

### Application Structure
```
StableDiffusion-Blazor-WebApp/
├── BlazorWebApp/
│   ├── Components/          # Blazor components
│   ├── Data/                # Database context, entities
│   ├── Models/              # Parameter models
│   ├── Pages/               # Blazor pages
│   ├── Services/            # Business logic services
│   ├── Workflows/           # ComfyUI workflow templates
│   ├── wwwroot/             # Static files (CSS, JS)
│   ├── Program.cs           # Application entry point
│   ├── appsettings.json     # Configuration (not in repo)
│   └── BlazorWebApp.csproj  # Project file
├── TestEndpoint/            # Test project
├── DOC/                     # Documentation (this folder)
└── BlazorWebApp.sln         # Solution file
```

### Output Structure
```
OutputDir/
├── txt2img-images/          # Txt2img samples
├── txt2img-grids/           # Txt2img grids
├── img2img-images/          # Img2img samples
├── img2img-grids/           # Img2img grids
├── extras-images/           # Upscaled images
└── img2vid-videos/          # Generated videos
```

### Resources Structure
```
ResourcesPath/
├── Stable-diffusion/        # Checkpoint models
├── Lora/                    # LoRA models
├── embeddings/              # Textual inversions
├── VAE/                     # VAE models
├── esrgan/                  # Upscaler models
├── hypernetworks/           # Hypernetwork models
└── controlnet/              # ControlNet models
```

---

## Configuration Options

### appsettings.json Reference

**Full Configuration**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "BlazorWebApp": "Debug"
    },
    "Console": {
      "FormatterName": "simple",
      "FormatterOptions": {
        "SingleLine": true,
        "IncludeScopes": true,
        "TimestampFormat": "yyyy-MM-dd HH:mm:ss "
      }
    }
  },
  
  "AllowedHosts": "*",
  
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=BlazorWebApp.db"
  },
  
  "OutputDir": "C:/StableDiffusion/outputs",
  "ResourcesPath": "C:/StableDiffusion/models",
  "ResourcePreviewsPath": "C:/StableDiffusion/previews",
  "ComfyUIPath": "C:/ComfyUI",
  
  "CivitaiApiToken": "",
  
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5000"
      },
      "Https": {
        "Url": "https://localhost:5001"
      }
    }
  }
}
```

### Environment Variables

Can override config with environment variables:

```bash
# Windows
set OutputDir=D:/MyOutputs
set CivitaiApiToken=your-token

# Linux/Mac
export OutputDir=/home/user/outputs
export CivitaiApiToken=your-token
```

---

## Troubleshooting

### Common Issues

#### 1. Cannot Connect to WebUI/ComfyUI

**Symptoms**: "Backend not available" message

**Solutions**:
- Verify WebUI/ComfyUI is running
- Check URLs in browser: `http://localhost:7860` and `http://localhost:8188`
- Ensure `--api` flag is set for WebUI
- Check firewall settings
- Verify ports are not in use by other applications

#### 2. Database Errors

**Symptoms**: "Database error" or "Cannot open database"

**Solutions**:
```bash
# Delete and recreate database
rm BlazorWebApp.db
dotnet ef database update

# Or drop and recreate
dotnet ef database drop
dotnet ef database update
```

#### 3. File Path Errors

**Symptoms**: "Path not found" or "Access denied"

**Solutions**:
- Verify all paths in `appsettings.json` exist
- Check path syntax (use forward slashes)
- Ensure application has write permissions
- Create missing directories manually

#### 4. Model Not Loading

**Symptoms**: Model not appearing in dropdown

**Solutions**:
- Refresh models in WebUI/ComfyUI first
- Restart Blazor app
- Check model files exist in configured paths
- Verify file permissions

#### 5. Generation Fails

**Symptoms**: Error during image generation

**Solutions**:
- Check WebUI/ComfyUI console for errors
- Verify model is loaded
- Check VRAM availability
- Reduce batch size or resolution
- Check `payload.json` for request details

#### 6. Out of Memory

**Symptoms**: "Out of memory" or application crashes

**Solutions**:
- Reduce batch size
- Lower resolution
- Close other applications
- Use smaller models
- Enable VAE tiling in WebUI

---

## Performance Optimization

### Application Settings

1. **Database**:
   ```csharp
   // In Program.cs
   opt.EnableSensitiveDataLogging(false);  // Production
   ```

2. **Caching**:
   - Tag cache refreshes every 30 minutes
   - Adjust in Program.cs if needed

3. **SignalR**:
   ```csharp
   opt.MaximumReceiveMessageSize = 100 * 1024 * 1024;  // 100 MB
   ```

### WebUI Settings

In WebUI settings:
- Unload models when not in use
- Enable xformers (faster)
- Use `--medvram` or `--lowvram` flags if needed
- Enable VAE tiling for large images

### System Optimization

1. **GPU**:
   - Update NVIDIA drivers
   - Close other GPU applications
   - Monitor VRAM usage

2. **Storage**:
   - Use SSD for models and outputs
   - Regular cleanup of old outputs
   - Compress old images

3. **RAM**:
   - Close unnecessary applications
   - Increase virtual memory if needed

---

## Backup and Maintenance

### Regular Backups

**Critical Data**:
```
BlazorWebApp.db              # Database (projects, metadata)
appsettings.json             # Configuration
Workflows/Templates/         # Custom workflows
```

**Backup Script** (PowerShell):
```powershell
$backupDir = "C:/Backups/BlazorDiffusion"
$date = Get-Date -Format "yyyy-MM-dd"

Copy-Item "BlazorWebApp.db" "$backupDir/BlazorWebApp-$date.db"
Copy-Item "appsettings.json" "$backupDir/appsettings-$date.json"
```

### Database Maintenance

```bash
# Backup database
sqlite3 BlazorWebApp.db ".backup BlazorWebApp-backup.db"

# Vacuum database (optimize)
sqlite3 BlazorWebApp.db "VACUUM;"

# Check integrity
sqlite3 BlazorWebApp.db "PRAGMA integrity_check;"
```

### Cleanup Old Files

```bash
# Remove old outputs (older than 30 days)
find $OutputDir -type f -mtime +30 -delete

# Remove orphaned preview images
# (Run resource audit in app)
```

---

## Security Considerations

### Local Deployment

**Current Security Posture**:
- ❌ No authentication
- ❌ No authorization
- ✅ HTTPS support
- ✅ Input validation (partial)
- ⚠️ Designed for single-user local use

**Recommendations**:
1. **Do not expose to internet** without authentication
2. Use firewall to restrict access to localhost
3. Keep CivitAI API token secure
4. Regular software updates
5. Antivirus/anti-malware protection

### Multi-User Deployment (Not Supported)

If modifying for multi-user:
- Implement ASP.NET Core Identity
- Add authorization policies
- File access controls
- Rate limiting
- API key management
- Input sanitization
- SQL injection prevention
- XSS protection

---

## Updating the Application

### Update from Git

```bash
git pull origin main
dotnet restore
dotnet build
dotnet ef database update  # Apply migrations
```

### Update Dependencies

```bash
# Check outdated packages
dotnet list package --outdated

# Update specific package
dotnet add package MudBlazor --version X.X.X

# Update all packages
dotnet restore
```

---

## Docker Deployment (Advanced)

**Dockerfile** (example):
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BlazorWebApp/BlazorWebApp.csproj", "BlazorWebApp/"]
RUN dotnet restore "BlazorWebApp/BlazorWebApp.csproj"
COPY . .
WORKDIR "/src/BlazorWebApp"
RUN dotnet build "BlazorWebApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BlazorWebApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlazorWebApp.dll"]
```

**docker-compose.yml**:
```yaml
version: '3.8'
services:
  blazor-diffusion:
    build: .
    ports:
      - "5000:5000"
    volumes:
      - ./data:/app/data
      - ./outputs:/app/outputs
    environment:
      - OutputDir=/app/outputs
      - ASPNETCORE_URLS=http://+:5000
```

**Note**: Requires network access to WebUI/ComfyUI running on host.

---

## Support and Resources

### Documentation
- This DOC folder
- Inline XML documentation
- README.md

### External Resources
- Automatic1111 WebUI: https://github.com/AUTOMATIC1111/stable-diffusion-webui
- ComfyUI: https://github.com/comfyanonymous/ComfyUI
- CivitAI: https://civitai.com
- MudBlazor: https://mudblazor.com

### Community
- GitHub Issues: Report bugs and feature requests
- Discussions: Ask questions
- Wiki: Community guides (if available)

---

This completes the deployment and setup guide. Follow these steps for successful installation and configuration of Blazor Diffusion.
