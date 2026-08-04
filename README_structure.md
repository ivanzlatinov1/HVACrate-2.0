# Структура на проекта HVACrate2

```
HVACrate2/
│
├── CLAUDE.md                      # контекст за Claude Code (чете се автоматично)
│
├── docs/
│   ├── decisions.md                # лог на взети решения ("защо")
│   ├── plan.md                     # roadmap по фази, checkbox-style
│   └── session-log.md              # кратко резюме след всяка сесия
│
├── HVACrate2.sln                   # .NET solution файл
│
├── src/
│   ├── HVACrate2.Core/              # чиста логика, без UI (class library)
│   │   ├── HVACrate2.Core.csproj
│   │   ├── Geometry/
│   │   │   ├── DirectionCalculator.cs   # ъгъл -> посока (С/СИ/И/...)
│   │   │   └── WallExtractor.cs         # LINE/LWPOLYLINE -> дължини по посока
│   │   ├── Openings/
│   │   │   ├── OpeningExtractor.cs      # W/D Marker -> широчина/височина/посока
│   │   │   └── OpeningGrouper.cs        # групиране по (широчина,височина)->бр.
│   │   ├── Excel/
│   │   │   └── ExcelWriter.cs           # запис в шаблона (ClosedXML)
│   │   └── Models/
│   │       ├── FloorConfig.cs
│   │       └── Opening.cs
│   │
│   └── HVACrate2.App/               # WPF desktop приложение
│       ├── HVACrate2.App.csproj
│       ├── App.xaml / App.xaml.cs
│       ├── MainWindow.xaml / .xaml.cs
│       ├── Views/
│       │   └── FloorPreviewControl.xaml   # 2D canvas за визуална проверка
│       └── ViewModels/
│           └── MainViewModel.cs
│
├── tests/
│   └── HVACrate2.Core.Tests/        # unit тестове върху Core логиката
│       └── HVACrate2.Core.Tests.csproj
│
├── samples/                        # примерни DXF/Excel файлове за валидация
│   ├── Brizstroy_Misari.dxf
│   └── ЕЕ_МИСАРИ.xlsx
│
└── .gitignore                      # bin/, obj/, publish/
```

## Логика на разделянето

- **`HVACrate2.Core`** съдържа цялата бизнес логика (четене на DXF,
  изчисления, запис в Excel) като независима библиотека, без никаква
  зависимост от UI. Това позволява unit тестване без нужда от WPF, и
  улеснява бъдеща смяна/добавяне на друг UI слой, ако потрябва.
- **`HVACrate2.App`** е тънък WPF слой, който само визуализира и
  извиква методи от `Core`.
- **`samples/`** пази реалния тестов проект (DXF + ръчно попълнения
  Excel), за да може при всяка промяна в логиката бързо да се провери
  дали резултатите все още съвпадат с познатите верни стойности.

## Стартови команди (след `dotnet new` инициализация)

```
dotnet new sln -n HVACrate2
dotnet new classlib -n HVACrate2.Core -o src/HVACrate2.Core -f net10.0
dotnet new wpf -n HVACrate2.App -o src/HVACrate2.App -f net10.0-windows
dotnet new xunit -n HVACrate2.Core.Tests -o tests/HVACrate2.Core.Tests -f net10.0

dotnet sln add src/HVACrate2.Core src/HVACrate2.App tests/HVACrate2.Core.Tests
dotnet add src/HVACrate2.App reference src/HVACrate2.Core
dotnet add tests/HVACrate2.Core.Tests reference src/HVACrate2.Core

dotnet add src/HVACrate2.Core package netDxf
dotnet add src/HVACrate2.Core package ClosedXML
```
