.PHONY: format check bundle backend-format backend-check backend-test

format: backend-format
	cd mobile && npm run format

check: backend-check
	cd mobile && npm run format:check && npm run typecheck && npm run lint && npm test

bundle:
	cd mobile && npm run build:pdf && npx expo export --platform android --platform web --max-workers 1

DOTNET := $(if $(wildcard $(HOME)/.dotnet/dotnet),$(HOME)/.dotnet/dotnet,dotnet)

backend-format:
	cd backend && DOTNET_PROCESSOR_COUNT=2 $(DOTNET) format UniCompanion.sln --no-restore

backend-check:
	cd backend && DOTNET_PROCESSOR_COUNT=2 $(DOTNET) restore UniCompanion.sln --locked-mode
	cd backend && DOTNET_PROCESSOR_COUNT=2 $(DOTNET) format UniCompanion.sln --verify-no-changes --no-restore
	cd backend && DOTNET_PROCESSOR_COUNT=2 $(DOTNET) build UniCompanion.sln --no-restore --disable-build-servers -m:1
	python3 -m unittest discover -s backend/tests/python -v

backend-test:
	python3 backend/scripts/dev.py test
