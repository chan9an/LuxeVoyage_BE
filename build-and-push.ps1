# LuxeVoyage — Build locally and push to Azure Container Registry
# Run from Backend/StayEasyNew directory:
#   cd Backend/StayEasyNew
#   .\build-and-push.ps1
#
# Prerequisites: Docker Desktop must be running

$ACR = "luxevoyageacr.azurecr.io"

Write-Host "`n=== Logging into ACR ===" -ForegroundColor Cyan
az acr login --name luxevoyageacr

Write-Host "`n=== Building Auth.API ===" -ForegroundColor Cyan
docker build -t "$ACR/auth-api:latest" -f Services/Auth.API/Dockerfile .

Write-Host "`n=== Building Hotel.API ===" -ForegroundColor Cyan
docker build -t "$ACR/hotel-api:latest" -f Services/Hotel.API/Dockerfile .

Write-Host "`n=== Building Booking.API ===" -ForegroundColor Cyan
docker build -t "$ACR/booking-api:latest" -f Services/Booking.API/Dockerfile .

Write-Host "`n=== Building Notification.Worker ===" -ForegroundColor Cyan
docker build -t "$ACR/notification-worker:latest" -f Services/Notification.Worker/Dockerfile .

Write-Host "`n=== Building ApiGateway ===" -ForegroundColor Cyan
docker build -t "$ACR/api-gateway:latest" -f ApigateWay/ApiGateway/Dockerfile .

Write-Host "`n=== Building Ai.API ===" -ForegroundColor Cyan
docker build -t "$ACR/ai-api:latest" -f Services/Ai.API/Dockerfile Services/Ai.API/

Write-Host "`n=== Pushing all images ===" -ForegroundColor Cyan
docker push "$ACR/auth-api:latest"
docker push "$ACR/hotel-api:latest"
docker push "$ACR/booking-api:latest"
docker push "$ACR/notification-worker:latest"
docker push "$ACR/api-gateway:latest"
docker push "$ACR/ai-api:latest"

Write-Host "`n=== All done! ===" -ForegroundColor Green
az acr repository list --name luxevoyageacr --output table
