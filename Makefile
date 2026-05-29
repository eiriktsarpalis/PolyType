SOURCE_DIRECTORY := $(dir $(realpath $(lastword $(MAKEFILE_LIST))))
ARTIFACT_PATH := $(SOURCE_DIRECTORY)artifacts
DOCS_PATH := $(SOURCE_DIRECTORY)docs
CONFIGURATION ?= Release
ADDITIONAL_ARGS ?= -p:ContinuousIntegrationBuild=true
NUGET_SOURCE ?= "https://api.nuget.org/v3/index.json"
NUGET_API_KEY ?= ""
DOCKER_IMAGE_NAME ?= "polytype-docker-build"
DOCKER_CMD ?= make pack
VERSION_FILE = $(SOURCE_DIRECTORY)version.json
VERSION ?= ""

clean:
	dotnet clean --configuration $(CONFIGURATION)
	rm -rf $(ARTIFACT_PATH)/*
	rm -rf $(DOCS_PATH)/api

restore:
	dotnet tool restore
	dotnet restore

build: restore
	dotnet build --no-restore --configuration $(CONFIGURATION) $(ADDITIONAL_ARGS)

test-clr: build
	dotnet test \
		--configuration $(CONFIGURATION) \
		--no-build \
		$(ADDITIONAL_ARGS) \
		-p:SkipTUnitTestRuns=true \
		--report-trx \
		--coverage \
		--coverage-output-format cobertura \
		--crashdump \
		--hangdump \
		--hangdump-timeout 10m \
		--results-directory $(ARTIFACT_PATH)/testResults

test-aot: build
	dotnet publish $(SOURCE_DIRECTORY)/tests/PolyType.Tests.NativeAOT/PolyType.Tests.NativeAOT.csproj \
		$(ADDITIONAL_ARGS) \
		-o $(ARTIFACT_PATH)/native-aot-tests \
	&& \
	$(ARTIFACT_PATH)/native-aot-tests/PolyType.Tests.NativeAOT

# Publishes the canonical Native AOT scenario used to track app size and
# compares the published binary's file size against the per-platform baseline
# committed at tests/SizeTrackingApp.AOT/aot-size-baselines.json. The tool
# prints an agent-parseable "AOT-SIZE-RESULT" block to stdout - a contributor
# can read each CI leg's log and copy the per-RID size_bytes values into the
# baselines file to refresh every platform from one CI run.
test-aot-size:
	dotnet publish $(SOURCE_DIRECTORY)/tests/SizeTrackingApp.AOT/SizeTrackingApp.AOT.csproj \
		--configuration $(CONFIGURATION) \
		$(ADDITIONAL_ARGS) \
		-o $(ARTIFACT_PATH)/size-tracking-app \
	&& \
	dotnet run --project $(SOURCE_DIRECTORY)/eng/AotSizeCheck/AotSizeCheck.csproj -- check \
		--baselines $(SOURCE_DIRECTORY)/tests/SizeTrackingApp.AOT/aot-size-baselines.json \
		--publish-dir $(ARTIFACT_PATH)/size-tracking-app \
		--app-name SizeTrackingApp.AOT

# Refreshes the baseline entry for the current platform's RID only.
# Use this for local single-platform updates; to update every platform at
# once, read the AOT-SIZE-RESULT block from each leg of a CI run and merge
# the size_bytes values into aot-size-baselines.json by hand.
update-aot-size-baseline:
	dotnet publish $(SOURCE_DIRECTORY)/tests/SizeTrackingApp.AOT/SizeTrackingApp.AOT.csproj \
		--configuration $(CONFIGURATION) \
		$(ADDITIONAL_ARGS) \
		-o $(ARTIFACT_PATH)/size-tracking-app \
	&& \
	dotnet run --project $(SOURCE_DIRECTORY)/eng/AotSizeCheck/AotSizeCheck.csproj -- update \
		--baselines $(SOURCE_DIRECTORY)/tests/SizeTrackingApp.AOT/aot-size-baselines.json \
		--publish-dir $(ARTIFACT_PATH)/size-tracking-app \
		--app-name SizeTrackingApp.AOT

test: test-clr test-aot

pack: build
	dotnet pack --no-restore --configuration Release $(ADDITIONAL_ARGS)

push:
	dotnet nuget push $(ARTIFACT_PATH)/*.nupkg -s $(NUGET_SOURCE) -k $(NUGET_API_KEY)

generate-docs: restore
	dotnet build -c Release
	dotnet docfx $(DOCS_PATH)/docfx.json --warningsAsErrors true

serve-docs: generate-docs
	dotnet docfx serve $(ARTIFACT_PATH)/_site --port 8080

release: restore
	test -n "$(VERSION)" || (echo "must specify VERSION" && exit 1)
	git diff --quiet && git diff --cached --quiet || (echo "repo contains uncommitted changes" && exit 1)
	dotnet nbgv set-version $(VERSION)
	git commit -m "Bump version to $(VERSION)"
	dotnet nbgv tag
	git push && git push --tags
	gh release create "`git describe --tags --abbrev=0`" --generate-notes --verify-tag

docker-build: clean
	docker build -t $(DOCKER_IMAGE_NAME) . && \
	docker run --rm -t \
		-v $(ARTIFACT_PATH):/repo/artifacts \
		$(DOCKER_IMAGE_NAME) \
		$(DOCKER_CMD)

	docker rmi -f $(DOCKER_IMAGE_NAME)

.DEFAULT_GOAL := test
