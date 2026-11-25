# منطق پروژه — صفحه به صفحه (پنل مشتریان AbrIT)

این فایل برای ساخت تصویر یا ماکاپ هر صفحه است. مرجع ظاهر: `docs/prototype/index.html` و `docs/DESIGN.md`.

> **نکته‌ی تولید تصویر:** مدل‌های تولید تصویر متن فارسی را معمولاً خراب می‌نویسند. برای هر صفحه دو راه دارید:
> (۱) تصویر را **بدون متن** بسازید و متن را بعداً در فیگما یا Canva اضافه کنید، یا
> (۲) از پیش‌نمایش HTML اسکرین‌شات بگیرید؛ دقیق‌تر است و برای ویدئوی تحویل هم مناسب‌تر است.
> پرامپت‌ها انگلیسی‌اند، چون مدل‌های تصویر با انگلیسی بهتر کار می‌کنند.

**سبک مشترک برای همه‌ی پرامپت‌ها** (به انتهای هر پرامپت اضافه کنید):
`Clean enterprise SaaS web UI, Cloudscape design system style, right-to-left (RTL) Persian layout, dark navy top navigation bar (#0F1B2A), light gray page background (#F4F4F4), white cards with 16px rounded corners and subtle shadow, primary blue #0972D3 pill-shaped buttons, Vazirmatn-like sans-serif font, flat, high fidelity, 1440x900 desktop screenshot, no device frame`

---

## بخش ۱ — منطق کلی

### نقش‌ها
| نقش | چه کاری می‌کند |
|---|---|
| مهمان | سرویس‌ها و پلن‌ها را می‌بیند، ثبت‌نام یا ورود می‌کند |
| مشتری (مشتری الف و مشتری ب) | سفارش ثبت می‌کند، فقط سفارش‌های خودش را می‌بیند، سفارش پرداخت‌نشده‌اش را لغو می‌کند |
| مدیر آزمایشی | همه‌ی سفارش‌ها را می‌بیند، پرداخت را تأیید می‌کند، تلاش مجدد می‌زند، سفارش را لغو می‌کند، قیمت پلن‌ها را ویرایش می‌کند |
| سرویس Nextcloud (Mock یا Real) | حساب Drive می‌سازد و سهمیه را تنظیم می‌کند |

### داده‌ها
- **سرویس (Service):** Drive، ERP360، Desk و پشتیبانی IT. محتوای قابل ویرایش در پنل ادمین است.
- **پلن (Plan):** Starter و Team. قیمت، سهمیه و تعداد کاربر دارد.
- **سفارش (Order):** مالک، پلن، مبلغ (در لحظه‌ی ثبت فریز می‌شود)، وضعیت و تاریخچه.
- **درخواست فعال‌سازی (ProvisioningRequest):** برای هر سفارش فقط یکی. شناسه‌ی حساب خارجی، تعداد تلاش‌ها و آخرین خطا را نگه می‌دارد.

### چرخه‌ی وضعیت سفارش
```
ثبت سفارش ──▶ در انتظار تأیید پرداخت ──(مدیر تأیید می‌کند)──▶ پرداخت‌شده ──▶ در حال فعال‌سازی
                     │                                                    │
                     └──(لغو)──▶ لغوشده                    ┌── موفق ──▶ فعال (یا «فعال (آزمایشی – Mock)»)
                                                           └── خطا ──▶ خطا در فعال‌سازی ──(تلاش مجدد، حداکثر ۵ بار)──▶ در حال فعال‌سازی
```
قانون‌ها:
- سفارش لغوشده یا پرداخت‌نشده هیچ‌وقت سرویس نمی‌گیرد.
- تأیید یا Callback دوباره سرویس دوم نمی‌سازد.
- اگر سرویس واقعاً ساخته نشده باشد، هیچ‌جا «فعال» واقعی نشان داده نمی‌شود.

### مسیر اصلی کاربر (همان سناریوی بند ۲ تسک)
D7/D8 ورود یا ثبت‌نام → D1 سرویس‌ها → D2 انتخاب پلن → D3 ثبت سفارش → D4 داشبورد («در انتظار تأیید پرداخت») →
A1 مدیر پرداخت را تأیید می‌کند → فعال‌سازی خودکار → D5 مشتری سرویس فعال را می‌بیند.
اگر فعال‌سازی خطا بدهد: D6 → A1 تلاش مجدد → D5.

### اجزای مشترک همه‌ی صفحه‌ها
- **نوار بالا (تیره):** لوگوی ABRIT در راست. لینک‌های «سرویس‌ها»، «داشبورد من» و «پشتیبانی». در چپ، نام کاربر و «خروج»، یا برای مهمان «ورود / ثبت‌نام».
- **فوتر:** «محیط ارزیابی — هیچ خرید واقعی انجام نمی‌شود».
- **موبایل (۳۷۵px):** منوی همبرگری، کارت‌ها زیر هم، و جدول‌ها به کارت تبدیل می‌شوند.

---

## بخش ۲ — صفحه‌ها

### D1 — فهرست سرویس‌ها `/services`
- **چه کسی:** همه، حتی مهمان.
- **هدف:** معرفی سرویس‌های ابریت و هدایت به Drive.
- **محتوا:** مسیر صفحه «خانه / سرویس‌ها»، عنوان «سرویس‌های ابریت»، و چهار کارت:
  - **AbrIT Drive:** قابل سفارش، دکمه‌ی آبی «مشاهده پلن‌ها»
  - **ERP360:** برچسب خاکستری «به‌زودی»، بدون دکمه
  - **AbrIT Desk:** لینک «اطلاعات بیشتر»
  - **پشتیبانی IT:** لینک به abrit.ir
  - زیر کارت‌ها یک هشدار آبی «محیط ارزیابی، قیمت‌ها آزمایشی‌اند»
- **منطق:** کارت‌ها از محتوای Service در Orchard خوانده می‌شوند. نوع کارت را فیلد Availability تعیین می‌کند.
- **کلیک‌ها:** «مشاهده پلن‌ها» ← D2.
- **پرامپت تصویر:** `Services catalog page: page title top-right, a row of four equal service cards; first card (rightmost) has a cloud icon and a solid blue "view plans" button; second card has a chart icon and a small gray "coming soon" tag; third card has a monitor icon and a blue text link; fourth card has a headset icon and an external text link; a light-blue info banner below the cards.`

### D2 — AbrIT Drive و پلن‌ها `/services/drive`
- **چه کسی:** همه. برای سفارش باید وارد شده باشد.
- **محتوا:**
  - **کارت Starter:** ۹۹٬۰۰۰ تومان در ماه، یک کاربر، ۱ گیگابایت
  - **کارت Team:** ۲۹۹٬۰۰۰ تومان در ماه، ۳ کاربر × ۵۰۰ مگابایت. حاشیه‌ی آبی و برچسب «پیشنهاد برای تیم‌ها» دارد.
  - زیر هر کارت: «مبالغ آزمایشی‌اند»
  - کادر «چطور کار می‌کند؟» با ۴ مرحله‌ی شماره‌دار
- **منطق:** پلن‌ها از محتوای Plan خوانده می‌شوند که مدیر می‌تواند قیمتشان را عوض کند. مهمانی که «انتخاب این پلن» را بزند به ورود می‌رود و بعد برمی‌گردد.
- **کلیک‌ها:** «انتخاب این پلن» ← D3.
- **پرامپت تصویر:** `Product plans page: title and short description at top-right; two pricing cards side by side, the left one highlighted with a thick blue border and a small "recommended" tag; each card shows plan name, large price, three checkmark features, a full-width button and a tiny gray note; below, a white panel with four numbered step tiles in a row.`

### D3 — بازبینی و ثبت سفارش `/orders/new?plan=…`
- **چه کسی:** فقط مشتری وارد شده.
- **محتوا:**
  - **کادر «خلاصه‌ی سفارش»:** سرویس، پلن، دوره، ظرفیت، مالک سفارش و مبلغ، در شبکه‌ی ۳ ستونه. نوار آبی کم‌رنگ «مبلغ قابل پرداخت (آزمایشی)». دکمه‌های «ثبت سفارش» و «انصراف».
  - **کادر کناری «بعد از ثبت سفارش»:** ۴ مرحله‌ی بعدی را توضیح می‌دهد.
- **منطق (AC-02):** با POST سفارش ساخته می‌شود:
  - مالک = کاربر فعلی
  - مبلغ و سهمیه از پلن کپی و فریز می‌شود
  - وضعیت = «در انتظار تأیید پرداخت»
  - سرور بررسی می‌کند پلن وجود داشته باشد و قابل سفارش باشد. فرم توکن ضد CSRF دارد.
- **کلیک‌ها:** «ثبت سفارش» ← D4 با پیام موفقیت.
- **پرامپت تصویر:** `Order review page: two-column layout; wide right column is a white card titled "order summary" with a 3-column grid of small gray labels and values, a light-blue total bar with a large price, and a blue primary button plus a text link; narrow left column is a white card with a short numbered list of next steps.`

### D4 — داشبورد مشتری `/dashboard`
- **چه کسی:** مشتری وارد شده.
- **محتوا:**
  - **۴ کاشی آمار:** کل سفارش‌ها، سرویس فعال (سبز)، در انتظار پرداخت (زرد)، نیاز به پیگیری (قرمز).
  - **جدول «سفارش‌های من»:** شماره سفارش، سرویس و پلن، مبلغ، تاریخ شمسی، برچسب وضعیت رنگی، و عملیات («مشاهده»، یا «لغو سفارش» فقط برای سفارش در انتظار پرداخت).
  - دکمه‌ی «سفارش جدید».
- **منطق (AC-07):** فقط سفارش‌هایی که مالکشان کاربر فعلی است، با فیلتر سمت سرور. اگر سفارشی نیست، پیام خالی و دکمه‌ی «مشاهده سرویس‌ها» نشان داده می‌شود.
- **کلیک‌ها:** شماره سفارش ← D5 یا D6.
- **پرامپت تصویر:** `Customer dashboard: four small white stat tiles in a row with big numbers (one green, one amber, one red); below, a white card titled "my orders" with an outlined button, containing a clean data table with gray header row, order IDs as blue links, colored status pills (red error, amber pending, brown "mock active"), and text-link actions.`

### D5 — جزئیات سرویس فعال (Mock) `/dashboard/orders/{id}`
- **چه کسی:** فقط مالک سفارش. برای دیگران 404 برمی‌گردد.
- **محتوا:**
  - عنوان «سفارش ABR-…» به‌همراه برچسب قهوه‌ای «فعال (آزمایشی – Mock)»
  - هشدار نارنجی-قهوه‌ای که می‌گوید این حساب Mock است و به سرور واقعی وصل نیست
  - **کادر «اطلاعات سفارش»:** پلن، مبلغ، وضعیت پرداخت (شبیه‌سازی‌شده)، تاریخ، حالت اتصال
  - **کادر «دسترسی به AbrIT Drive»:** نام کاربری، آدرس ورود، شناسه‌ی سرویس خارجی، نوار سهمیه «۰ از ۱ گیگابایت»، و راهنمای تنظیم رمز از طریق ایمیل. **رمز هیچ‌وقت نمایش داده نمی‌شود.**
  - **ستون «تاریخچه‌ی وضعیت»:** خط زمانی از ثبت تا فعال‌سازی
- **منطق (AC-04):** در حالت Mock یک حساب ساختگی با سهمیه‌ی ۱ گیگابایت ثبت شده. برچسب Mock اجباری است.
- **پرامپت تصویر:** `Service detail page: title with an order ID and a small brown status pill; a full-width soft orange warning banner; two-column layout: wide right column with two white cards (order info key-value grid; access info with key-value grid, an empty thin progress bar and a light-blue info note), narrow left card with a vertical timeline of four colored status pills with timestamps.`

### D6 — جزئیات سفارش با خطا `/dashboard/orders/{id}`
- **همان چیدمان D5، با این تفاوت‌ها:**
  - برچسب قرمز «خطا در فعال‌سازی»
  - هشدار قرمز «ساخت فضای Drive انجام نشد؛ نیازی به ثبت دوباره نیست»
  - کادر دسترسی خالی است
  - تعداد تلاش‌ها: «۱ از ۵»
- **منطق (AC-06):** خطای سرویس ذخیره و به زبان ساده نمایش داده می‌شود، بدون جزئیات فنی یا رمز. مشتری دکمه‌ی تلاش مجدد ندارد. تلاش مجدد فقط با مدیر است.
- **پرامپت تصویر:** `Same layout as the service detail page but in an error state: red status pill next to the title, a full-width soft red error banner, the access card shows only a short gray placeholder sentence, the timeline's top item is a red pill.`

### D7 — ورود `/Login` · D8 — ثبت‌نام `/Register` · D9 — بازیابی رمز `/ForgotPassword`
- **چه کسی:** مهمان.
- **منطق (AC-01):** همه از ماژول Users خود Orchard هستند و فقط ظاهرشان عوض شده. ایمیل بازیابی رمز در محیط توسعه به smtp4dev می‌رود. خروج با «خروج» در نوار بالا انجام می‌شود.
- **محتوا:**
  - **D7:** کارت وسط صفحه با «ایمیل یا نام کاربری»، «رمز عبور»، لینک فراموشی رمز و ثبت‌نام، و دکمه‌ی «ورود».
  - **D8:** نام کاربری، ایمیل، رمز و تکرار رمز با راهنمای قوانین رمز.
  - **D9:** ایمیل، دکمه‌ی «ارسال لینک بازیابی»، و پیام سبز بعد از ارسال.
- **پرامپت تصویر:** `Minimal sign-in page: dark navy top bar, centered single white card 440px wide with a bold title, a gray subtitle, two labeled text inputs with rounded borders, two small blue text links on one row, and a full-width blue pill button.`

### A1 — مدیریت سفارش‌ها (ادمین) `/Admin/AbrIT/Orders`
- **چه کسی:** فقط نقش مدیر. برای کاربر عادی دسترسی رد می‌شود (AC-08).
- **محتوا:**
  - پیام نتیجه‌ی آخرین اقدام (سبز یا قرمز)
  - کادر «همه‌ی سفارش‌ها» با حالت اتصال (Mock یا Real) در زیرعنوان
  - جست‌وجو و فیلترها: همه، در انتظار، فعال، خطا، لغوشده
  - **جدول:** شماره، ایمیل مشتری، پلن، مبلغ، وضعیت، تلاش‌ها، عملیات
- **عملیات بسته به وضعیت:**

| وضعیت | دکمه‌ها |
|---|---|
| در انتظار تأیید پرداخت | «تأیید پرداخت» (آبی) و «لغو» |
| خطا در فعال‌سازی | «تلاش مجدد» (حاشیه‌دار) تا سقف ۵ بار |
| فعال | فقط «جزئیات» |

- **منطق:**
  - **AC-03:** تأیید پرداخت ← پرداخت‌شده ← فعال‌سازی ← فعال یا خطا.
  - **AC-05:** تأیید دوباره هیچ اثری ندارد و سرویس دوم ساخته نمی‌شود.
- **پرامپت تصویر:** `Admin orders management page: dark top bar labeled as admin, a green success banner, a white card with a search input and a row of filter chips (first chip filled blue), and a wide data table with order IDs, emails, plan names, prices, colored status pills, attempt counters and action buttons (blue "approve" pill, outlined "retry" pill, text links).`

### A2 — تنظیمات اتصال Drive (ادمین) `/Admin/AbrIT/Provisioning`
- **چه کسی:** مدیر.
- **محتوا:**
  - **کادر «وضعیت اتصال»:** فقط‌خواندنی. حالت Mock یا Real، آدرس سرور، کاربر مدیر Nextcloud، و رمز برنامه که فقط «تنظیم شده یا نشده» نشان داده می‌شود و هرگز خود رمز نه. حداکثر تلاش، آخرین درخواست، و راهنمای رفتن به حالت Real.
  - **کادر «شبیه‌سازی خطا (فقط Mock)»:** کلید روشن و خاموش و تعداد فراخوانی‌های ناموفق.
- **منطق:** حالت اتصال از متغیرهای محیطی می‌آید و از UI عوض نمی‌شود. کلید خطا فقط برای نمایش AC-06 در دمو است.
- **پرامپت تصویر:** `Admin settings page: two white cards side by side; the wide one shows a read-only key-value grid with a small "Mock" tag and a light-blue info note; the narrow one has a toggle switch, a small numeric input and a blue save button.`

### صفحه‌های آماده‌ی Orchard (طراحی جدا لازم ندارند)
- **ویرایش سرویس و پلن در پنل ادمین:** ویرایشگر محتوای خود Orchard. مدیر قیمت و سهمیه را عوض می‌کند. سفارش‌های قبلی تغییر نمی‌کنند، چون مبلغ در سفارش فریز شده.
- **مدیریت کاربران و نقش‌ها:** امکانات خود Orchard.
- **ایمیل بازیابی رمز:** در رابط smtp4dev دیده می‌شود.

---

## بخش ۳ — ترتیب پیشنهادی تصاویر برای ویدئو یا اسکرین‌شات تحویل (AC-02 تا AC-06)
1. **D3:** مشتری الف پلن Starter را سفارش می‌دهد.
2. **D4:** سفارش با وضعیت «در انتظار تأیید پرداخت» دیده می‌شود.
3. **A1:** مدیر پرداخت را تأیید می‌کند.
4. **D5:** وضعیت «فعال (آزمایشی – Mock)» است و سهمیه ۱ گیگابایت.
5. **A1:** «تأیید» یا Callback دوباره زده می‌شود و هیچ سرویس جدیدی ساخته نمی‌شود.
6. **A2:** شبیه‌سازی خطا روشن می‌شود.
7. **A1:** تأیید سفارش جدید.
8. **D6:** وضعیت «خطا در فعال‌سازی».
9. **A2:** شبیه‌سازی خاموش می‌شود.
10. **A1:** «تلاش مجدد» زده می‌شود.
11. **D5:** سرویس فعال می‌شود.
