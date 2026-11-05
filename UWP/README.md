# Touchpad PS4 — UWP

Interface UWP em XAML, Acrylic com fallback opaco, preferências persistentes e componente desktop em segundo plano. Destino: Windows desktop 10 (1809+) e 11, x64. O componente full-trust é necessário para o ícone na área de notificação, atalho global, leitura contínua do controle e mouse fora da janela. Este pacote não se destina a Xbox ou outras famílias sem Win32.

## Usar

- Ative/desative pela janela, pelo menu do ícone ou por Ctrl+Alt+M.
- Ajuste a sensibilidade (0,2× a 3×) e a velocidade extra em gestos rápidos (0% a +200%). A sensibilidade define o deslocamento; a velocidade extra acelera gestos rápidos e preserva a precisão dos lentos. O padrão é 1× e 0%, com botão para restaurá-lo. Preferências antigas recebem velocidade extra de 0%.
- Esses ajustes são aplicados apenas aos eventos gerados a partir do DualShock. Não alteram velocidade, aceleração ou outras configurações do mouse do Windows, nem interceptam movimentos do mouse físico. Ambos continuam controlando o mesmo cursor.
- Configure também clique por toque e rolagem com dois dedos.
- Clique físico no touchpad: esquerdo. Segure L1 para o botão direito.
- Com “Manter na área de notificação” ligado, fechar a janela mantém o motor ativo. Abra novamente pelo ícone. O Windows decide se o ícone aparece diretamente ou na seta de ícones ocultos.
- Ao minimizar pelo botão padrão, a janela permanece na barra de tarefas. A opção “Ocultar na bandeja” encerra a interface e mantém o auxiliar ativo; o ícone abre a interface novamente.
- Com a opção desligada, o auxiliar encerra após a janela deixar de enviar sinais por aproximadamente 8 segundos; isso também pode acontecer se o Windows suspender a interface minimizada.
- “Sair” no menu do ícone encerra o motor. “Encerrar mouse e aplicativo” encerra ambos.
- Não há inicialização automática com o Windows nesta versão.

## Compilar e gerar o instalador

1. Instale Visual Studio com a carga de trabalho de desenvolvimento UWP, ferramentas C# UWP e Windows SDK **10.0.19041.0**. O componente desktop usa o compilador do .NET Framework incluído no Windows.
2. Abra `TouchpadPS4.sln`, restaure o NuGet e selecione x64.
3. Para gerar a distribuição assinada, execute no Windows PowerShell: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-Installer.ps1`.
4. Distribua a pasta `Installer` inteira, incluindo dependências, scripts e o certificado público. A chave privada permanece no repositório de certificados do usuário que compilou; não é exportada.
5. No destino, execute `Instalar.cmd` como administrador, confira o certificado e confirme digitando SIM. Alternativamente, com um certificado já confiável, execute `Install.ps1` sem parâmetros. O aplicativo fica disponível no menu Iniciar e pode ser removido pelas Configurações do Windows.

O formato gerado pelas ferramentas UWP é APPX, instalado pelo mecanismo de pacotes do Windows, também usado por MSIX. Para publicação pública, use assinatura apropriada ou a Microsoft Store; `runFullTrust` fica sujeito à análise da Store. O certificado autoassinado é destinado à distribuição local.

## Estrutura e validação

- `MainPage.xaml`: interface de vidro.
- `MainPage.xaml.cs`: preferências, estado e inicialização do auxiliar.
- `Tray/TrayApp.cs`: ícone, atalho, reconexão e ciclo de vida.
- `Tray/Engine.cs`: motor HID separado dos arquivos originais, com liberação dos cliques ao desconectar, cancelamento da leitura antes de reconectar e status USB.
- `Build-Tray.ps1`: compila apenas o componente desktop.
- `Build-Installer.ps1`: gera e assina a distribuição.
- `Test-Engine.ps1`: verifica gestos, liberação de cliques, sensibilidade, velocidade extra, precisão e valores inválidos com a injeção de mouse substituída por um registro em memória.

O projeto original na pasta superior foi preservado. A validação física requer um DualShock 4. Detecção inclui os produtos Sony 09CC e 05C4; a compatibilidade com clones não foi validada. A compilação UWP requer as ferramentas descritas acima; compilar apenas o auxiliar não valida a interface nem o pacote.
