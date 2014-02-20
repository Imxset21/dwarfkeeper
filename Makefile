# Dwarfkeeper
MONO_DIR = /opt/monodevelop
MONO_BIN_DIR = $(MONO_DIR)/bin

MONO_BIN = $(MONO_BIN_DIR)/mono
MONO_MCS = $(MONO_BIN_DIR)/mcs

# Make this blank to disable debugging symbols
DEBUG ?= -debug

DWARFKEEPER_LIB = DwarfClient.cs

EXECUTABLES = DwarfCLI.exe DwarfServer.exe
ISIS = Isis.cs

.PHONY: all clean build

all: build

DwarfCLI.exe: %.exe : %.cs
	$(MONO_MCS) $(DEBUG) $< $(ISIS) $(DWARFKEEPER_LIB) -out:$@

DwarfServer.exe: %.exe : %.cs
	$(MONO_MCS) $(DEBUG) $< $(ISIS) -out:$@

build: $(EXECUTABLES)

clean:
	@rm -f $(EXECUTABLES)
