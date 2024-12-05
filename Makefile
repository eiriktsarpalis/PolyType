SOURCE_DIRECTORY := $(dir $(realpath $(lastword $(MAKEFILE_LIST))))
ARTIFACT_PATH := $(SOURCE_DIRECTORY)artifacts
DOCS_PATH := $(SOURCE_DIRECTORY)docs
CONFIGURATION ?= Release
NUGET_SOURCE ?= "https://api.nuget.org/v3/index.json"
NUGET_API_KEY ?= ""
ADDITIONAL_ARGS ?= -p:ContinuousIntegrationBuild=true
CODECOV_ARGS ?= --collect "Code Coverage;Format=cobertura" --results-directory $(ARTIFACT_PATH)
DOCKER_IMAGE_NAME ?= "polytype-docker-build"
DOCKER_CMD ?= make CONFIGURATION=$(CONFIGURATION)
VERSION_FILE = $(SOURCE_DIRECTORY)version.json
VERSION ?= ""

clean:
	rm -rf $(ARTIFACT_PATH)/*
	rm -rf $(DOCS_PATH)/api

build: clean
	dotnet build -c $(CONFIGURATION) $(ADDITIONAL_ARGS)

test: build
	dotnet test -c $(CONFIGURATION) $(ADDITIONAL_ARGS) $(CODECOV_ARGS)

pack: test
	dotnet pack -c $(CONFIGURATION) $(ADDITIONAL_ARGS)

generate-docs: clean
	dotnet tool update -g docfx
	docfx $(DOCS_PATH)/docfx.json

bump-version:
	test -n "$(VERSION)" || (echo "must specify VERSION" && exit 1)
	git diff --quiet && git diff --cached --quiet || (echo "repo contains uncommitted changes" && exit 1)
	sed -i 's/"version"\s*:\s*"[^"]*"/"version": "$(VERSION)"/' $(VERSION_FILE)
	git add $(VERSION_FILE) && git commit -m "Bump version to $(VERSION)"
	git tag $(VERSION)

push:
	for nupkg in `ls $(ARTIFACT_PATH)/*.nupkg`; do \
		dotnet nuget push $$nupkg -s $(NUGET_SOURCE) -k $(NUGET_API_KEY); \
	done

docker-build: clean
	docker build -t $(DOCKER_IMAGE_NAME) . && \
	docker run --rm -t \
		-v $(ARTIFACT_PATH):/repo/artifacts \
		$(DOCKER_IMAGE_NAME) \
		$(DOCKER_CMD)

	docker rmi -f $(DOCKER_IMAGE_NAME)

.DEFAULT_GOAL := pack