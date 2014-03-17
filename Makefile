# Dwarfkeeper
MONO_DIR = /opt/monodevelop
MONO_BIN_DIR = $(MONO_DIR)/bin

MONO_BIN = $(MONO_BIN_DIR)/mono
MONO_MCS = $(MONO_BIN_DIR)/mcs

# Make this blank to disable debugging symbols
DEBUG ?= -debug

DWARFCLIENT_LIB = DwarfClient.cs 
DWARFKEEPER_LIB = DwarfCMD.cs DwarfData.cs
DWARFSERVER_LIB = 
EXECUTABLES = DwarfCLI.exe DwarfServer.exe
ISIS = Isis.cs

.PHONY: all clean build rebuild

all: build

DwarfCLI.exe: %.exe : %.cs
	$(MONO_MCS) $(DEBUG) $< $(ISIS) $(DWARFCLIENT_LIB) $(DWARFKEEPER_LIB) -out:$@ -main:DwarfCLI.DwarfCLI

# TODO: Add compiler flag to DwarfTree to remove need for -main flag here
DwarfServer.exe: %.exe : %.cs
	$(MONO_MCS) $(DEBUG) $< $(ISIS) $(DWARFSERVER_LIB) $(DWARFKEEPER_LIB) -out:$@ -main:DwarfServer.DwarfServer

build: $(EXECUTABLES)

clean:
	@rm -f $(EXECUTABLES) $(addsuffix .mdb, $(EXECUTABLES))

rebuild: clean build
