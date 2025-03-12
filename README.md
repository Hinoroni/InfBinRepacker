# InfBinRepacker
This application allows for the import and export of data stored in an archive used by Dotemu in Might & Magic: Clash of Heroes - Definitive Edition. This archive consists of a file named _data.inf_ which defines the file system structure and a file named _data.bin_ that contains concatenated data.<br/>
More information on this format [here](https://encode.su/threads/4364-Can-you-help-me-identify-this-algorithm).

```
Description:
  Import and export files of a ".inf"/".bin" file system

Usage:
  InfBinRepacker [commands] [options]

Commands:
  -e /--export                   Export all files from the file system
  -es/--export-single     [arg]  Export a single file from the file system
  -i /--import                   Import files from the output directory
  -c /--create                   Create a new ".inf"/".bin" file system from the files in the output directory
  -p /--print                    Print the content of the ".inf" file

Options:
  -if/--inf-file          [arg]  Path to the ".inf" file - Default: input/data.inf
  -bf/--bin-file          [arg]  Path to the ".bin" file - Default: input/data.bin
  -od/--output-directory  [arg]  Output directory - Default: output
  -cl/--compression-level [arg]  LZ4 compression level of the files between 0 and 12 - Default: 0
  -kc/--keep-compressed          Skip the compression when importing and the decompression when exporting
  -s /--seed              [arg]  32bits xxHash seed used to calculate checksums - Default: 477478303
  -h /--help                     Display help
```
Regarding the `--keep-compressed` option, the file system requires the uncompressed size to decompress files. So if you use this option to import an edited file you also need provide the uncompressed version.<br/>
Example: when importing "_ouput/compressed/data/myfile.dds_" the application will try to find "_ouput/decompressed/data/myfile.dds_" to get the uncompressed size.<br/>
Otherwise, the file system will keep the original uncompressed size which will cause the game to crash.
