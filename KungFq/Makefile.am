
EXTRA_DIST =  

# Warning: This is an automatically generated file, do not edit!

if ENABLE_DEBUG_X86
ASSEMBLY_COMPILER_COMMAND = gmcs
ASSEMBLY_COMPILER_FLAGS =  -noconfig -codepage:utf8 -warn:4 -optimize- -debug "-define:DEBUG"
ASSEMBLY = bin/Debug/KungFq.exe
ASSEMBLY_MDB = $(ASSEMBLY).mdb
COMPILE_TARGET = exe
PROJECT_REFERENCES = 
BUILD_DIR = bin/Debug

KUNGFQ_EXE_MDB_SOURCE=bin/Debug/KungFq.exe.mdb
KUNGFQ_EXE_MDB=$(BUILD_DIR)/KungFq.exe.mdb
ICSHARPCODE_SHARPZIPLIB_DLL_SOURCE=lib/ICSharpCode.SharpZipLib.dll
SEVENZIPSHARP_DLL_SOURCE=lib/SevenZipSharp.dll

endif

if ENABLE_RELEASE_X86
ASSEMBLY_COMPILER_COMMAND = gmcs
ASSEMBLY_COMPILER_FLAGS =  -noconfig -codepage:utf8 -warn:4 -optimize-
ASSEMBLY = bin/Release/KungFq.exe
ASSEMBLY_MDB = 
COMPILE_TARGET = exe
PROJECT_REFERENCES = 
BUILD_DIR = bin/Release

KUNGFQ_EXE_MDB=
ICSHARPCODE_SHARPZIPLIB_DLL_SOURCE=lib/ICSharpCode.SharpZipLib.dll
SEVENZIPSHARP_DLL_SOURCE=lib/SevenZipSharp.dll

endif

AL=al2
SATELLITE_ASSEMBLY_NAME=$(notdir $(basename $(ASSEMBLY))).resources.dll

PROGRAMFILES = \
	$(KUNGFQ_EXE_MDB) \
	$(ICSHARPCODE_SHARPZIPLIB_DLL) \
	$(SEVENZIPSHARP_DLL)  

BINARIES = \
	$(KUNGFQ)  


RESGEN=resgen2
	
all: $(ASSEMBLY) $(PROGRAMFILES) $(BINARIES) 

FILES = \
	Main.cs \
	AssemblyInfo.cs \
	Options.cs \
	IFastqDeCompresser.cs \
	FastqReader.cs \
	FastqDeCompresser.cs \
	EncodedFastqReader.cs \
	IIdDeCompresser.cs \
	PlainIdDeCompresser.cs \
	SraIdDeCompresser.cs \
	EncodeIdDeCompresser.cs \
	FastqCutoffReader.cs \
	PlaceholderIdGenerator.cs \
	ICoder.cs \
	CommandLineParser.cs \
	CRC.cs \
	OutBuffer.cs \
	InBuffer.cs \
	RangeCoderBitTree.cs \
	RangeCoderBit.cs \
	RangeCoder.cs \
	IMatchFinder.cs \
	LzInWindow.cs \
	LzBinTree.cs \
	LzOutWindow.cs \
	LzmaBase.cs \
	LzmaDecoder.cs \
	LzmaEncoder.cs \
	LzmaStream.cs \
	IQualityCounter.cs \
	DummyQualityCounter.cs \
	QualityCounter.cs \
	HistogramDrawer.cs 

DATA_FILES = 

RESOURCES = 

EXTRAS = \
	lib \
	lib/ICSharpCode.SharpZipLib.dll \
	kungfq.in 

REFERENCES =  \
	System \
	System.Core \
	System.Xml.Linq \
	System.Xml

DLL_REFERENCES =  \
	lib/ICSharpCode.SharpZipLib.dll \
	lib/SevenZipSharp.dll

CLEANFILES = $(PROGRAMFILES) $(BINARIES) 

include $(top_srcdir)/Makefile.include

ICSHARPCODE_SHARPZIPLIB_DLL = $(BUILD_DIR)/ICSharpCode.SharpZipLib.dll
SEVENZIPSHARP_DLL = $(BUILD_DIR)/SevenZipSharp.dll
KUNGFQ = $(BUILD_DIR)/kungfq

$(eval $(call emit-deploy-target,ICSHARPCODE_SHARPZIPLIB_DLL))
$(eval $(call emit-deploy-target,SEVENZIPSHARP_DLL))
$(eval $(call emit-deploy-wrapper,KUNGFQ,kungfq,x))


$(eval $(call emit_resgen_targets))
$(build_xamlg_list): %.xaml.g.cs: %.xaml
	xamlg '$<'

$(ASSEMBLY_MDB): $(ASSEMBLY)

$(ASSEMBLY): $(build_sources) $(build_resources) $(build_datafiles) $(DLL_REFERENCES) $(PROJECT_REFERENCES) $(build_xamlg_list) $(build_satellite_assembly_list)
	mkdir -p $(shell dirname $(ASSEMBLY))
	$(ASSEMBLY_COMPILER_COMMAND) $(ASSEMBLY_COMPILER_FLAGS) -out:$(ASSEMBLY) -target:$(COMPILE_TARGET) $(build_sources_embed) $(build_resources_embed) $(build_references_ref)
