SOURCE_DIRECTORY := $(dir $(realpath $(lastword $(MAKEFILE_LIST))))
ARTIFACT_PATH := $(SOURCE_DIRECTORY)artifacts
DOCS_PATH := $(SOURCE_DIRECTORY)docs
CONFIGURATION ?= Release
NUGET_SOURCE ?= "https://api.nuget.org/v3/index.json"
NUGET_API_KEY ?= ""
ADDITIONAL_ARGS ?= -p:ContinuousIntegrationBuild=true -warnAsError -warnNotAsError:NU1901,NU1902,NU1903,NU1904
CODECOV_ARGS ?= --collect "Code Coverage;Format=cobertura" --results-directory $(ARTIFACT_PATH)
DOCKER_IMAGE_NAME ?= "polytype-docker-build"
DOCKER_CMD ?= make pack
VERSION_FILE = $(SOURCE_DIRECTORY)version.json
VERSION ?= ""

clean:
	rm -rf $(ARTIFACT_PATH)/*
	rm -rf $(DOCS_PATH)/api

build: clean
	dotnet build -c $(CONFIGURATION) $(ADDITIONAL_ARGS)

test: build
	dotnet test --no-build -c $(CONFIGURATION) $(ADDITIONAL_ARGS) $(CODECOV_ARGS)

pack: test
	dotnet pack -c $(CONFIGURATION) $(ADDITIONAL_ARGS)

push:
	dotnet nuget push $(ARTIFACT_PATH)/*.nupkg -s $(NUGET_SOURCE) -k $(NUGET_API_KEY)

restore-tools:
	dotnet tool restore

generate-docs: restore-tools build
	dotnet docfx $(DOCS_PATH)/docfx.json

serve-docs: generate-docs
	dotnet docfx serve $(ARTIFACT_PATH)/_site --port 8080

release: restore-tools
	test -n "$(VERSION)" || (echo "must specify VERSION" && exit 1)
	git diff --quiet && git diff --cached --quiet || (echo "repo contains uncommitted changes" && exit 1)
	dotnet nbgv set-version $(VERSION)
	git commit -m "Bump version to $(VERSION)"
	dotnet nbgv tag

docker-build: clean
	docker build -t $(DOCKER_IMAGE_NAME) . && \
	docker run --rm -t \
		-v $(ARTIFACT_PATH):/repo/artifacts \
		$(DOCKER_IMAGE_NAME) \
		$(DOCKER_CMD)

	docker rmi -f $(DOCKER_IMAGE_NAME)

.DEFAULT_GOAL := test