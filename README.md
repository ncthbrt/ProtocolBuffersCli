# ProtocolBuffersCli
Protocol Buffers are great. Figuring out the baroque sequence of commands used to compile a folder containing *.proto files isn't. This project wraps the protocol buffers compiler in a friendlier CLI

Currently uses v3.2.0 of the protocol buffers compiler

## Usage

This tool wraps Google's Protocol buffer compilers so that it is easier to use. 
Note that it repects folder hierarchy, so if two proto files are stored in 
/proto/A.proto' and '/proto/dir/B.proto' respectively, the output will be 
/proto/A.cs and /proto/dir/B.cs when run from '/'.
 
 The current options are supported:
 
```    - build [options] [target_directory]:  
    
        --output=           The root folder where compiled files will be placed
        --lang=             The output language. Options are [java,csharp,python, go]
        --namespace=        If this option is specified, the output file will 
                            be arranged in a hierarchy matching their namespace
                            from that specified in the source folder
        --file_extension=   Custom file extension to add to generated files,
                            eg. --file_extension=.g.cs will result in files 
                            ending in *.g.cs
        target_directory    Root directory to recursively look for proto files.
                            This will usually be the proto project folder       
```
