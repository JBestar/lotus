using System.Text.Json;
using LotusOddEven.App.Config;
using LotusOddEven.Core.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LotusOddEven.App.Services;

public sealed class GameSnapshotDto
{
    public int? TimerSeconds { get; set; }
    public string? OpenCard { get; set; }
    public string? HiddenCard { get; set; }
    public bool ShuffleDetected { get; set; }
    public int? RoundId { get; set; }
}

/// <summary>
/// Executes JavaScript inside WebView2 to read game state and click bet buttons.
/// </summary>
public sealed class WebViewBridgeService
{
    private readonly IGameDomSelectors _selectors;
    private WebView2? _webView;

    public WebViewBridgeService(IGameDomSelectors selectors)
    {
        _selectors = selectors;
    }

    public void Attach(WebView2 webView) => _webView = webView;

    public async Task EnsureCoreAsync()
    {
        if (_webView is null)
            throw new InvalidOperationException("WebView2가 연결되지 않았습니다.");
        await _webView.EnsureCoreWebView2Async();
    }

    public async Task NavigateAsync(string url)
    {
        await EnsureCoreAsync();
        _webView!.CoreWebView2.Navigate(url);
    }

    public void Reload() => _webView?.CoreWebView2?.Reload();

    public async Task<GameSnapshotDto?> PollGameStateAsync()
    {
        var script = BuildPollScript();
        var raw = await ExecuteScriptAsync(script);
        if (string.IsNullOrWhiteSpace(raw) || raw == "null")
            return null;

        // ExecuteScriptAsync returns a JSON-encoded string literal.
        var unquoted = JsonSerializer.Deserialize<string>(raw);
        if (string.IsNullOrWhiteSpace(unquoted))
            return null;

        return JsonSerializer.Deserialize<GameSnapshotDto>(unquoted, JsonOptions);
    }

    public async Task ClickBetAsync(BetSide side, int amount)
    {
        var buttonSelector = side switch
        {
            BetSide.Under => _selectors.BetUnderButtonSelector,
            BetSide.Over => _selectors.BetOverButtonSelector,
            BetSide.Red => _selectors.BetRedButtonSelector,
            BetSide.Black => _selectors.BetBlackButtonSelector,
            _ => throw new ArgumentOutOfRangeException(nameof(side))
        };

        var amountSel = EscapeJs(_selectors.ChipAmountInputSelector);
        var btnSel = EscapeJs(buttonSelector);
        var script = $$"""
            (() => {
              const amountEl = document.querySelector('{{amountSel}}');
              if (amountEl) {
                amountEl.value = '{{amount}}';
                amountEl.dispatchEvent(new Event('input', { bubbles: true }));
                amountEl.dispatchEvent(new Event('change', { bubbles: true }));
              }
              const btn = document.querySelector('{{btnSel}}');
              if (btn) { btn.click(); return true; }
              return false;
            })();
            """;

        await ExecuteScriptAsync(script);
    }

    public async Task<string> ExecuteScriptAsync(string script)
    {
        await EnsureCoreAsync();
        return await _webView!.CoreWebView2.ExecuteScriptAsync(script);
    }

    private string BuildPollScript()
    {
        var timer = EscapeJs(_selectors.TimerSelector);
        var open = EscapeJs(_selectors.OpenCardSelector);
        var hidden = EscapeJs(_selectors.HiddenCardSelector);
        var shuffle = EscapeJs(_selectors.ShuffleIndicatorSelector);
        var round = EscapeJs(_selectors.RoundIdSelector);

        return $$"""
            (() => {
              const q = (sel) => {
                for (const part of sel.split(',')) {
                  const el = document.querySelector(part.trim());
                  if (el) return el;
                }
                return null;
              };
              const text = (el) => el ? (el.getAttribute('data-card') || el.textContent || '').trim() : null;
              const timerEl = q('{{timer}}');
              const openEl = q('{{open}}');
              const hiddenEl = q('{{hidden}}');
              const shuffleEl = q('{{shuffle}}');
              const roundEl = q('{{round}}');
              const timerText = timerEl ? (timerEl.textContent || '').replace(/[^0-9]/g, '') : '';
              const roundText = roundEl ? (roundEl.textContent || '').replace(/[^0-9]/g, '') : '';
              const payload = {
                timerSeconds: timerText ? parseInt(timerText, 10) : null,
                openCard: text(openEl),
                hiddenCard: text(hiddenEl),
                shuffleDetected: !!(shuffleEl && (shuffleEl.getAttribute('data-active') === 'true' || shuffleEl.classList.contains('active'))),
                roundId: roundText ? parseInt(roundText, 10) : null
              };
              return JSON.stringify(payload);
            })();
            """;
    }

    /// <summary>
    /// Inject CSS outline on bead-plate circles that mark shoe starts (1판).
    /// Works by DOM scripting inside the live site — no native overlay window needed.
    /// </summary>
    /// <param name="zeroBasedIndices">Indices of bead cells that are shoe starts.</param>
    public async Task ApplyShoeStartOverlaysAsync(IReadOnlyList<int> zeroBasedIndices)
    {
        var sel = EscapeJs(_selectors.BeadPlateCellSelector);
        var indexCsv = string.Join(",", zeroBasedIndices);
        var script = $$"""
            (() => {
              const styleId = 'lotus-shoe-overlay-style';
              if (!document.getElementById(styleId)) {
                const style = document.createElement('style');
                style.id = styleId;
                style.textContent = `
                  .lotus-shoe-start {
                    outline: 3px solid #F59E0B !important;
                    outline-offset: 2px !important;
                    box-shadow: 0 0 0 2px rgba(245,158,11,0.45) !important;
                    border-radius: 50% !important;
                    position: relative !important;
                    z-index: 5 !important;
                  }
                  .lotus-shoe-start::after {
                    content: '1';
                    position: absolute;
                    top: -6px;
                    right: -6px;
                    background: #F59E0B;
                    color: #111;
                    font-size: 9px;
                    font-weight: 700;
                    line-height: 1;
                    padding: 1px 3px;
                    border-radius: 3px;
                  }
                `;
                document.head.appendChild(style);
              }

              const nodes = [];
              for (const part of '{{sel}}'.split(',')) {
                document.querySelectorAll(part.trim()).forEach(el => nodes.push(el));
                if (nodes.length) break;
              }

              const mark = new Set([{{indexCsv}}].filter(n => Number.isFinite(n)));
              nodes.forEach((el, i) => {
                if (mark.has(i)) el.classList.add('lotus-shoe-start');
                else el.classList.remove('lotus-shoe-start');
              });
              return nodes.length;
            })();
            """;

        await ExecuteScriptAsync(script);
    }

    private static string EscapeJs(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
