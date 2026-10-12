/* ==========================================================================
   quiz.js — 课程共用的自测组件（零依赖，可离线）
   两种题型：
     1) 单选题   <div class="quiz" data-quiz>          选项按钮 data-correct="true|false"
     2) 排序题   <div class="quiz" data-quiz="order">  按钮 data-order="1|2|3..."，按正确顺序点击
   反馈：点错立即给否定反馈并允许重试；点对锁定该题并累计得分。
   进度分存在内存中，刷新即重置（刻意不持久化，避免"看过答案"的错觉）。
   ========================================================================== */
(function () {
  "use strict";

  function all(sel, root) {
    return Array.prototype.slice.call((root || document).querySelectorAll(sel));
  }

  /* 每题一个计分状态；页面底部若有 [data-quiz-score] 则实时汇总 */
  var stats = { answered: 0, correct: 0, total: 0 };

  function refreshScoreboard() {
    var el = document.querySelector("[data-quiz-score]");
    if (!el) return;
    el.textContent =
      "已作答 " + stats.answered + " / " + stats.total +
      " 题，一次答对 " + stats.correct + " 题。";
  }

  function feedbackOf(quiz) {
    return quiz.querySelector(".quiz-feedback");
  }

  function setFeedback(quiz, ok, text) {
    var fb = feedbackOf(quiz);
    if (!fb) return;
    fb.textContent = text;
    fb.classList.toggle("is-ok", !!ok);
    fb.classList.toggle("is-no", !ok);
  }

  /* ---------------- 单选题 ---------------- */

  function initChoice(quiz) {
    var opts = all(".quiz-opt", quiz);
    var firstTry = true;
    var wrongPicks = 0;

    opts.forEach(function (btn, i) {
      var idx = btn.querySelector(".idx");
      if (idx && !idx.textContent.trim()) idx.textContent = String(i + 1);

      btn.addEventListener("click", function () {
        if (quiz.dataset.done === "1") return;
        var correct = btn.dataset.correct === "true";

        if (correct) {
          quiz.dataset.done = "1";
          btn.classList.add("is-ok");
          opts.forEach(function (o) { o.disabled = true; });
          stats.answered++;
          if (firstTry) stats.correct++;
          refreshScoreboard();
          setFeedback(
            quiz,
            true,
            (btn.dataset.why || "正确。") +
              (firstTry ? "" : "（这题你先错了 " + wrongPicks + " 次，记进错题更划算。）")
          );
        } else {
          firstTry = false;
          wrongPicks++;
          btn.classList.add("is-no");
          btn.disabled = true;
          setFeedback(quiz, false, btn.dataset.why || "不对，再想想：先回到上文的因果链。");
        }
      });
    });

    addReset(quiz, function () {
      firstTry = true;
      wrongPicks = 0;
      quiz.dataset.done = "0";
      opts.forEach(function (o) {
        o.disabled = false;
        o.classList.remove("is-ok", "is-no");
      });
      setFeedback(quiz, null, "");
    });
  }

  /* ---------------- 排序题（调用链排序） ---------------- */

  function initOrder(quiz) {
    var opts = all(".quiz-opt", quiz);
    var step = 0;
    var mistakes = 0;
    var total = opts.length;

    /* 打乱初始顺序，避免"位置就是答案" */
    var shuffled = opts.slice().sort(function () { return Math.random() - 0.5; });
    var box = quiz.querySelector(".quiz-opts");
    if (box) shuffled.forEach(function (o) { box.appendChild(o); });

    opts.forEach(function (btn) {
      var idx = btn.querySelector(".idx");
      if (idx) idx.dataset.slot = "";

      btn.addEventListener("click", function () {
        if (quiz.dataset.done === "1" || btn.disabled) return;
        var want = String(step + 1);

        if (btn.dataset.order === want) {
          step++;
          btn.classList.add("is-ok", "is-picked");
          btn.disabled = true;
          var i = btn.querySelector(".idx");
          if (i) i.dataset.slot = String(step);

          if (step === total) {
            quiz.dataset.done = "1";
            stats.answered++;
            if (mistakes === 0) stats.correct++;
            refreshScoreboard();
            setFeedback(
              quiz,
              true,
              "链路顺序正确。" + (mistakes === 0 ? "零失误，这条链已经记住了。" : "（错了 " + mistakes + " 次，建议再复述一遍。）")
            );
          } else {
            setFeedback(quiz, null, "第 " + step + " 步对了，继续点下一个。");
          }
        } else {
          mistakes++;
          var snapshot = btn.textContent;
          btn.classList.add("is-no");
          setFeedback(quiz, false, "这一步不该排在第 " + (step + 1) + " 位。想想谁调用谁：" + snapshot.trim());
          setTimeout(function () { btn.classList.remove("is-no"); }, 900);
        }
      });
    });

    addReset(quiz, function () {
      step = 0;
      mistakes = 0;
      quiz.dataset.done = "0";
      opts.forEach(function (o) {
        o.disabled = false;
        o.classList.remove("is-ok", "is-no", "is-picked");
        var i = o.querySelector(".idx");
        if (i) i.dataset.slot = "";
      });
      setFeedback(quiz, null, "");
    });
  }

  function addReset(quiz, fn) {
    var bar = quiz.querySelector(".quiz-actions");
    if (!bar) return;
    var btn = document.createElement("button");
    btn.type = "button";
    btn.className = "quiz-reset";
    btn.textContent = "重做本题";
    btn.addEventListener("click", fn);
    bar.appendChild(btn);
  }

  /* ---------------- 启动 ---------------- */

  document.addEventListener("DOMContentLoaded", function () {
    var quizzes = all("[data-quiz]");
    stats.total = quizzes.length;
    quizzes.forEach(function (q) {
      if (q.dataset.quiz === "order") initOrder(q);
      else initChoice(q);
    });
    refreshScoreboard();
  });
})();
