# Dwarfkeeper
MONO_DIR = /opt/monodevelop
MONO_BIN_DIR = $(MONO_DIR)/bin

MONO_BIN = $(MONO_BIN_DIR)/mono
MONO_MCS = $(MONO_BIN_DIR)/mcs

EXECUTABLES = DwarfClient.exe DwarfServer.exe
ISIS = Isis.cs

.PHONY: all clean build

all: build

$(EXECUTABLES): %.exe : %.cs
	$(MONO_MCS) $< $(ISIS) -out:$@

build: $(EXECUTABLES)

clean:
	@rm -f $(EXECUTABLES)
