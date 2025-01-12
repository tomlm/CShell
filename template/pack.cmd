pushd CShell.Template
erase /q *.nupkg
nuget pack CShell.Template.nuspec
popd
