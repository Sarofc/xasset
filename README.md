# xasset

 forked from <https://github.com/xasset/xasset>

>> Warning

    开发中，没有MGF无法使用。先占坑。
    MGF将在后续开源。

# Feature

- 一键出包，自定义打包流程。
- 作为MGF的子模块。

# TODO List

- VFS分文件支持，现在只支持4g单文件
- 优化打包设置
- 整合热更

# 流程图


```mermaid
graph TD;
    RequestRemoteVersionList --> LoadLocalVersionList --> GetDownloadAssetInfoList
    --> Download

```