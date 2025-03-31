SOURCE_DIRECTORY := $(dir $(realpath $(lastword $(MAKEFILE_LIST))))
ARTIFACT_PATH := $(SOURCE_DIRECTORY)artifacts
DOCS_PATH := $(SOURCE_DIRECTORY)docs
CONFIGURATION ?= Release
ADDITIONAL_ARGS ?= -p:ContinuousIntegrationBuild=true -warnAsError -warnNotAsError:NU1901,NU1902,NU1903,NU1904
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

test: build
	dotnet test --no-build --configuration $(CONFIGURATION) $(ADDITIONAL_ARGS) \
		--blame \
		--results-directory $(ARTIFACT_PATH)/testResults \
		--collect "Code Coverage;Format=cobertura" \
		--logger "trx" \
		-- \
		RunConfiguration.CollectSourceInformation=true

pack: test
	dotnet pack --configuration Release $(ADDITIONAL_ARGS)

push:
	dotnet nuget push $(ARTIFACT_PATH)/*.nupkg -s $(NUGET_SOURCE) -k $(NUGET_API_KEY)

generate-docs: clean restore
	dotnet build --no-restore --configuration Release $(ADDITIONAL_ARGS)
	dotnet docfx $(DOCS_PATH)/docfx.json

serve-docs: generate-docs
	dotnet docfx serve $(ARTIFACT_PATH)/_site --port 8080

release: restore
	test -n "$(VERSION)" || (echo "must specify VERSION" && exit 1)
	git diff --quiet && git diff --cached --quiet || (echo "repo contains uncommitted changes" && exit 1)
	dotnet nbgv set-version $(VERSION)
	git commit -m "Bump version to $(VERSION)"
	dotnet nbgv tag
	git push && git push --tags
	gh release create "`git describe --tags --abbrev=0`" --generate-notes --draft --verify-tag

docker-build: clean
	docker build -t $(DOCKER_IMAGE_NAME) . && \
	docker run --rm -t \
		-v $(ARTIFACT_PATH):/repo/artifacts \
		$(DOCKER_IMAGE_NAME) \
		$(DOCKER_CMD)

	docker rmi -f $(DOCKER_IMAGE_NAME)

.DEFAULT_GOAL := test